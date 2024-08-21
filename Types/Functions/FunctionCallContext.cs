using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Functions;

public delegate void MessageFunc(params object?[] args);

[PublicAPI]
public class FunctionCallContext:SendReceive{
	private static int _nextId;

	private static readonly ThreadLocal<FunctionCallContext?> ThreadLocal=new();
	private readonly CancellationTokenSource _cts=new();
	private readonly MessageFunc _send;
	private readonly Func<Task<string>> _caller;
	private readonly TaskCompletionSource<object?> _tcs;
	public readonly string? Method;

	public readonly string? Type;

	internal FunctionCallContext(string? type,string? method,MessageFunc send,TaskCompletionSource<object?> tcs,Func<Task<string>> caller){
		Type=type;
		Method=method;
		_send=send;
		_tcs=tcs;
		_caller=caller;
	}

	public override bool Finished=>_tcs.Task.IsCompleted;
	public override Task<object?> Task=>_tcs.Task;

	public CancellationToken CancellationToken=>_cts.Token;
	public void CancelSelf()=>_cts.Cancel();
	public void CancelSelfAfter(TimeSpan delay)=>_cts.CancelAfter(delay);
	public Task<string> GetCaller()=>_caller();


	public override void SendMessage(params object?[] args)=>_send(args);


	internal static PendingCall<T> CallFunction<T>(string? type,string? method,params object?[] args)=>CallFunction(type,method,args).Cast<T>();

	internal static PendingCall CallFunction(string? type,string? method,params object?[] args){
		if(type!=null){
			Invoker? local;
			lock(RegisteredTypes.Registered)
				if(!RegisteredTypes.Registered.TryGetValue(type,out local))
					local=null;
			if(local!=null){
				var action=ListenAllCalls.Broadcast(type,method,args);
				var pending=local.Call(type,method,args);
				if(action!=null) pending.Finally(action);
				return pending;
			}
		}

		var truth=new PendingCallRawData();

		var call=new PendingCall<object?>(truth);

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
			var len=buff.GetBufferAndLength().len;
			buff.WriteArray(args,buff.WriteDynamic,already);
			already.RemoveAll(o=>!DynamicData.NeedsFreeing(o));

			ListenAllCalls.Broadcast(type,method,buff,len);
		} catch(Exception e){
			call.Reject(e);
			return call;
		}

		var connection=ClientConnection.Instance;
		if(connection==null||(type!=null&&!Rpc.IsConnected)){
			call.Reject(new RpcConnectionException("Not connected"));
			return call;
		}
		truth.SendFunc=msgArgs=>{
			if(truth.Finished) return;
			var msg=new DataOutputBuff();
			msg.WriteByte((byte)PacketType.MessageToExecutor);
			msg.WriteLength(callId);
			var list=new List<object>();
			msg.WriteArray(msgArgs,msg.WriteDynamic,list);
			lock(already)
				already.AddRange(list.Where(DynamicData.NeedsFreeing));

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

	internal static void RunWithContext(Action func,FunctionCallContext context)
		=>RunWithContext<VoidType>(()=>{
			func();
			return default;
		},context);

	internal static T RunWithContext<T>(Func<T> func,FunctionCallContext context){
		var old=ThreadLocal.Value;
		ThreadLocal.Value=context;
		try{
			return func();
		} finally{
			ThreadLocal.Value=old;
		}
	}

	internal static async Task<object?> RunWithContextAsync(Func<object?> func,FunctionCallContext context,string? type,string? method,object?[]? args){
#pragma warning disable CS1998// Async method lacks 'await' operators and will run synchronously
		object? result=System.Threading.Tasks.Task.Run(async ()=>RunWithContext(func,context));
#pragma warning restore CS1998// Async method lacks 'await' operators and will run synchronously

		try{
			Type t;
			while(true)
				if(result is VoidType or null) return null;
				else if((t=result.GetType()).FullName=="System.Threading.Tasks.VoidTaskResult") return null;
				else if(result is Task task){
					await task;
					result=t.GetProperty("Result")?.GetValue(result);
				} else if(result is ValueTask valueTask){
					await valueTask;
					return null;
				} else if(t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(ValueTask<>))
					result=t.GetMethod(nameof(ValueTask<object>.AsTask))?.Invoke(result,[]);
				else return result;
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e).Append(type,method,args);
		}
	}


	public static FunctionCallContext GetContext()=>ThreadLocal.Value??throw new InvalidOperationException("FunctionCallContext not available");
}