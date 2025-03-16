using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Functions;

[PublicAPI]
public class PendingCall:IAsyncEnumerable<RpcDataPrimitive[]>{
	private protected readonly PendingCallRawData RawData;

	internal PendingCall(PendingCallRawData rawData){
		RawData=rawData;
	}

	public void Cancel()=>RawData.CancelFunc?.Invoke();

	public PendingCall WithCancellation(CancellationToken token){
		RawData.WithCancellation(token);
		return this;
	}

	public bool Finished=>RawData.Finished;
	public Task<RpcDataPrimitive> TaskRaw=>RawData.TaskRaw;



	public PendingCall SendMessage(params object?[] args){
		var already=new RpcDataPrimitive.Already(a=>Finally(a));
		return SendMessageRaw(RpcDataPrimitive.FromArray(args,already));
	}

	public PendingCall SendMessageRaw(RpcDataPrimitive[] args){
		RawData.SendFunc?.Invoke(args);
		return this;
	}

	public PendingCall AddMessageListener(Delegate a){
		RawData.MessageQueue.AddMessageListener(a);
		return this;
	}

	public PendingCall AddMessageListenerRaw(Action<RpcDataPrimitive[]> a){
		RawData.MessageQueue.AddMessageListenerRaw(a);
		return this;
	}

	public PendingCall AsForwarded(FunctionCallContext ctx){
		WithCancellation(ctx.CancellationToken);
		_=AddMessageListenerRaw(msg=>ctx.SendMessageRaw(msg));
		_=ctx.AddMessageListenerRaw(msg=>SendMessageRaw(msg));
		return this;
	}
	
	public PendingCall<TNew> Cast<TNew>()=>new(RawData);
	public PendingCallCasted Cast(Type type)=>new(RawData,type);

	public async Task ToTask()=>await TaskRaw;
	public async Task<TNew?> ToTask<TNew>()=>(await TaskRaw).To<TNew>();
	public async Task<object?> ToTask(Type type)=>(await TaskRaw).To(type);

	public static implicit operator Task(PendingCall call)=>call.TaskRaw;
	public static implicit operator ValueTask(PendingCall call)=>new(call.TaskRaw);
	public TaskAwaiter GetAwaiter()=>ToTask().GetAwaiter();

	public IAsyncEnumerator<RpcDataPrimitive[]> GetAsyncEnumerator(CancellationToken cancel=default)=>RawData.MessageQueue.GetAsyncEnumerator(cancel);

	public Task Then(Action a)=>TaskRaw.Then(a);
	public Task<TReturn> Then<TReturn>(Func<TReturn> a)=>TaskRaw.Then(a);
	public Task Catch(Action<Exception> a)=>TaskRaw.Catch(a);
	public Task Finally(Action a)=>TaskRaw.ContinueWith(_=>a());
}