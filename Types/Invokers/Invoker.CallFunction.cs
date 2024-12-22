using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Invokers;

public abstract partial class Invoker{
	private static int _nextId;

	#region CallFunction
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
				var pending=CallLocal(ctx=>local.Invoke(type,method,args,ctx),type,method,args);
				if(action!=null) pending.Finally(action);
				return pending;
			}
		}

		var rawData=new PendingCallRawData(type,method,args);

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
			var already=new Dictionary<RpcDataPrimitive,int>();
			buff.WriteArray(args,d=>d.Write(buff,already));
			foreach(var key in already.Keys)
				if(key.IsDisposable(out var action))
					toFree.Add(action);

			//ListenAllCalls is not needed here, as it gets sent to the server, and the server already listens on calls
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
	#endregion

	#region CallLocal
	internal static PendingCall<RpcDataPrimitive> CallLocal(Func<object?> f)=>CallLocal(_=>f());

	internal static PendingCall CallLocal(Action<FunctionCallContext> a)=>CallLocal(ctx=>{
		a(ctx);
		return null;
	});

	internal static PendingCall<RpcDataPrimitive> CallLocal(Func<FunctionCallContext,object?> a)=>CallLocal(a,null,null,null);

	private static PendingCall<RpcDataPrimitive> CallLocal(Func<FunctionCallContext,object?> a,string? type,string? method,RpcDataPrimitive[]? args){
		var rawData=new PendingCallRawData(type,method,args);
		var context=new FunctionCallContext(type,
			method,
			args,
			sending=>Task.Run(()=>rawData.MessageQueue.DoReceiveMessage(sending)),
			rawData.TaskCompletionSource,
			()=>Task.FromResult(Rpc.PrettyName));

		rawData.SendFunc=received=>{
			if(!rawData.Finished) Task.Run(()=>context.MessageQueue.DoReceiveMessage(received));
		};
		rawData.CancelFunc=()=>context.CancelSelf();


		var task=RunAndAwait(a,context,type,method,args);
		TaskToCall(task,rawData);
		return new PendingCall<RpcDataPrimitive>(rawData);
	}

	private static readonly Type VoidTaskResult=Type.GetType("System.Threading.Tasks.VoidTaskResult")!;

	internal static async Task<RpcDataPrimitive> RunAndAwait(Func<FunctionCallContext,object?> a,FunctionCallContext ctx,string? type,string? method,RpcDataPrimitive[]? args){
		try{
			//TODO remove RunWithContext completely 
#pragma warning disable CS0618// Type or member is obsolete
			var result=await Task.Run(()=>FunctionCallContext.RunWithContext(a,ctx)).ConfigureAwait(false);
#pragma warning restore CS0618// Type or member is obsolete
			while(true)
				if(result is VoidType or null||VoidTaskResult.IsInstanceOfType(result))
					return new RpcDataPrimitive();
				else if(result is ValueTask valueTask){
					await valueTask;
					return new RpcDataPrimitive();
				} else if(result is Task task){
					await task;
					result=task.GetType().GetProperty(nameof(Task<object>.Result))?.GetValue(result);
				} else if(result.GetType().Push(out var t).IsGenericType&&t.GetGenericTypeDefinition()==typeof(ValueTask<>))
					result=t.GetMethod(nameof(ValueTask<object>.AsTask))?.Invoke(result,[]);
				else return RpcDataPrimitive.From(result);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e).Append(type,method,args);
		}
	}

	private static async void TaskToCall(Task<RpcDataPrimitive> task,PendingCallRawData rawData){
		try{
			rawData.Resolve(await task);
		} catch(Exception e){
			rawData.Reject(e);
		}
	}
	#endregion

}