using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Invokers;

public abstract partial class Invoker{
	private static int _nextId;

	#region CallFunction
	internal static PendingCall<T> CallFunction<T>(string? type,string? method,params object?[] args)=>CallFunction(type,method,args).Cast<T>();

	internal static PendingCall CallFunction(string? type,string? method,params object?[] args){
		if(args.Any(o=>o is FunctionCallContext)){
			var call=CallFunction(type,method,args.Where(o=>o is not FunctionCallContext).ToArray());
			foreach(var context in args.OfType<FunctionCallContext>()) call.AsForwarded(context);
			return call;
		}

		List<Action>? list=null;
		var already=new RpcDataPrimitive.Already(a=>(list??=[]).Add(a));
		var pendingCall=CallFunctionRaw(type,method,RpcDataPrimitive.FromArray(args,already));
		if(list!=null) pendingCall.Finally(()=>list.ForEach(a=>a()));
		return pendingCall;
	}

	internal static PendingCall<RpcDataPrimitive> CallFunctionRaw(string? type,string? method,RpcDataPrimitive[] args){
		if(type!=null){
			Invoker? local;
			lock(RegisteredTypes.Registered)
				if(!RegisteredTypes.Registered.TryGetValue(type,out local))
					local=null;
			if(local!=null){
				ListenAllCalls.Broadcast(type,method,args);
				var pending=CallLocal(ctx=>local.Invoke(type,method,args,ctx),type,method,args);
				return pending;
			}
		}

		var rawData=new PendingCallRawData(type,method,args);

		var call=new PendingCall<RpcDataPrimitive>(rawData,null);

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
		return Task.FromResult(new RpcDataPrimitive());
	},null,null,null);

	internal static PendingCall<RpcDataPrimitive> CallLocal(Func<FunctionCallContext,object?> a)
		=>CallLocal(ctx=>RpcInvoker.ObjectToTask(a(ctx),null),null,null,null);

	private static PendingCall<RpcDataPrimitive> CallLocal(Func<FunctionCallContext,Task<RpcDataPrimitive>> a,string? type,string? method,RpcDataPrimitive[]? args){
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
		return new PendingCall<RpcDataPrimitive>(rawData,null);
	}

	private static readonly Type VoidTaskResult=Type.GetType("System.Threading.Tasks.VoidTaskResult")!;

	internal static async Task<RpcDataPrimitive> RunAndAwait(Func<FunctionCallContext,Task<RpcDataPrimitive>> a,FunctionCallContext ctx,string? type,string? method,RpcDataPrimitive[]? args){
		try{
			return await Task.Run(()=>a(ctx)).ConfigureAwait(false);
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