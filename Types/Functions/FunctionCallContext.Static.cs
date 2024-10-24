using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Functions;

public partial class FunctionCallContext{
	internal static PendingCall<T> CallFunction<T>(string? type,string? method,params object?[] args)=>CallFunction(type,method,args).Cast<T>();
	internal static PendingCall CallFunction(string? type,string? method,params object?[] args)=>CallFunctionRaw(type,method,RpcDataPrimitive.FromArray(args));

	internal static PendingCall<RpcDataPrimitive> CallFunctionRaw(string? type,string? method,RpcDataPrimitive[] args){
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

		var rawData=new PendingCallRawData();

		var call=new PendingCall<RpcDataPrimitive>(rawData);

		var toFree=new List<Action>();
		call.Finally(()=>toFree.ForEach(a=>a()));

		var buff=new DataOutputBuff();
		int callId;
		try{
			buff.WriteByte((byte)PacketType.FunctionCall);
			callId=Interlocked.Increment(ref _nextId);
			buff.WriteLength(callId);
			buff.WriteString(type);
			buff.WriteString(method);
			var len=buff.GetBufferAndLength().len;
			var already=new Dictionary<RpcDataPrimitive,int>();
			buff.WriteArray(args,d=>d.Write(buff,already));
			foreach(var key in already.Keys)
				if(key.IsDisposable(out var action))
					toFree.Add(action);

			ListenAllCalls.Broadcast(type,method,buff,len);
		} catch(Exception e){
			rawData.Reject(e);
			return call;
		}

		var connection=ClientConnection.Instance;
		if(connection==null||type!=null&&!Rpc.IsConnected){
			rawData.Reject(new RpcConnectionException("Not connected"));
			return call;
		}
		rawData.SendFunc=msgArgs=>{
			if(rawData.Finished) return;
			var msg=new DataOutputBuff();
			msg.WriteByte((byte)PacketType.MessageToExecutor);
			msg.WriteLength(callId);
			var already=new Dictionary<RpcDataPrimitive,int>();
			buff.WriteArray(msgArgs,d=>d.Write(buff,already));
			foreach(var key in already.Keys)
				if(key.IsDisposable(out var action))
					toFree.Add(action);

			connection.SendRaw(msg);
		};
		rawData.CancelFunc=()=>{
			if(rawData.Finished) return;
			var msg=new DataOutputBuff();
			msg.WriteByte((byte)PacketType.FunctionCancel);
			msg.WriteLength(callId);

			connection.SendRaw(msg);
		};

		connection.SendCall(callId,rawData,buff);

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

	internal static async Task<object?> RunWithContextAsync(Func<object?> func,FunctionCallContext context,string? type,string? method,RpcDataPrimitive[]? args){
#pragma warning disable CS1998// Async method lacks 'await' operators and will run synchronously
		object? result=Task.Run(async ()=>RunWithContext(func,context));
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
	public static bool TryGetContext(out FunctionCallContext ctx)=>ThreadLocal.Value.NotNull(out ctx!);
}