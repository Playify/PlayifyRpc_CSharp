using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Functions;

public delegate void MessageFunc(params object?[] args);

[PublicAPI]
public class FunctionCallContext:SendReceive{
	private static int _nextId;

	private static readonly ThreadLocal<FunctionCallContext?> ThreadLocal=new();
	private readonly CancellationTokenSource _cts=new();
	private readonly MessageFunc _send;
	private readonly TaskCompletionSource<object?> _tcs;
	public readonly string? Method;

	public readonly string? Type;

	internal FunctionCallContext(string? type,string? method,MessageFunc send,TaskCompletionSource<object?> tcs){
		Type=type;
		Method=method;
		_send=send;
		_tcs=tcs;
	}

	public override bool Finished=>_tcs.Task.IsCompleted;
	public override Task<object?> Task=>_tcs.Task;

	public CancellationToken CancellationToken=>_cts.Token;
	public void CancelSelf()=>_cts.Cancel();
	public void CancelSelfAfter(TimeSpan delay)=>_cts.CancelAfter(delay);


	public override void SendMessage(params object?[] args)=>_send(args);

	internal static PendingCall CallFunction(string? type,string method,object?[] args){
		if(type!=null){
			Invoker? local;
			lock(RegisteredTypes.Registered)
				if(!RegisteredTypes.Registered.TryGetValue(type,out local))
					local=null;
			if(local!=null) return local.Call(type,method,args);
		}

		var truth=new PendingCallRawData();

		var call=new PendingCall(truth);

		var already=new List<object>();
		call.Finally(()=>DynamicData.Free(already));

		var buff=new DataOutputBuff();
		int callId;
		try{
			buff.WriteByte((byte)PacketType.FunctionCall);
			callId=Interlocked.Increment(ref _nextId);
			buff.WriteLength(callId);
			buff.WriteString(type);
			buff.WriteString(method);
			buff.WriteArray(args,buff.WriteDynamic,already);
		} catch(Exception e){
			call.Reject(e);
			return call;
		}

		var connection=ClientConnection.Instance;
		if(connection==null||(type!=null&&!Rpc.IsConnected)){
			call.Reject(new Exception("Not connected"));
			return call;
		}
		truth.SendFunc=msgArgs=>{
			if(truth.Finished) return;
			var msg=new DataOutputBuff();
			msg.WriteByte((byte)PacketType.MessageToExecutor);
			msg.WriteLength(callId);
			var list=new List<object>();
			msg.WriteArray(msgArgs,msg.WriteDynamic,list);
			already.AddRange(list);

			connection.SendRaw(msg);
		};
		truth.CancelFunc=()=>{
			if(truth.Finished) return;
			var msg=new DataOutputBuff();
			msg.WriteByte((byte)PacketType.FunctionCancel);
			msg.WriteLength(callId);

			connection.SendRaw(msg);
		};

		connection.SendCall(callId,call,buff);

		return call;
	}

	internal static T RunWithContext<T>(Func<T> func,FunctionCallContext context){
		ThreadLocal.Value=context;
		try{
			return func();
		} finally{
			ThreadLocal.Value=null;
		}
	}

	public static FunctionCallContext GetContext()=>ThreadLocal.Value??throw new InvalidOperationException("FunctionCallContext not available");
}