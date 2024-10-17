using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Functions;

internal class PendingCallRawData:SendReceive{
	internal readonly TaskCompletionSource<RpcDataPrimitive> TaskCompletionSource=new();
	internal Action? CancelFunc;
	internal MessageFunc? SendFunc;

	public override bool Finished=>TaskCompletionSource.Task.IsCompleted;
	public override Task<RpcDataPrimitive> Task=>TaskCompletionSource.Task;

	public override void SendMessage(params RpcDataPrimitive[] args){
		if(Finished) return;
		SendFunc?.Invoke(args);
	}

	public void SendCancel(){
		if(Finished) return;
		CancelFunc?.Invoke();
	}
}

[PublicAPI]
public abstract class PendingCall:SendReceive{
	public void Cancel()=>_rawData.SendCancel();

	public PendingCall WithCancellation(CancellationToken token){
		if(Finished) return this;
		var registration=token.Register(_rawData.SendCancel);
		Finally(registration.Dispose);

		return this;
	}

	#region Internal
	private readonly PendingCallRawData _rawData;

	private protected PendingCall(PendingCallRawData rawData)=>_rawData=rawData;

	internal void Resolve(RpcDataPrimitive o)=>_rawData.TaskCompletionSource.TrySetResult(o);
	internal void Reject(Exception e)=>_rawData.TaskCompletionSource.TrySetException(e is RpcException rpc?rpc.Unfreeze():e);
	#endregion

	#region SendReceive
	public override bool Finished=>_rawData.Finished;
	public override Task<RpcDataPrimitive> Task=>_rawData.Task;

	public override void SendMessage(params RpcDataPrimitive[] args)=>_rawData.SendMessage(args);

	public override void AddMessageListener(MessageFunc a)=>_rawData.AddMessageListener(a);

	internal override void DoReceiveMessage(RpcDataPrimitive[] args)=>_rawData.DoReceiveMessage(args);
	#endregion

	#region Task
	public static implicit operator Task<object?>(PendingCall call)=>call is PendingCallCasted c?c.ToTask():call.ToTask<object?>();
	public static implicit operator Task(PendingCall call)=>call.Task;
	public TaskAwaiter<object?> GetAwaiter()=>ToTask<object?>().GetAwaiter();

	public async Task<object?> ToTask(Type t)=>(await Task).To(t);
	public async Task<T> ToTask<T>()=>(await Task).To<T>();

	public Task Then<T>(Action<T> a)=>ToTask<T>().Then(a);
	public Task<TReturn> Then<T,TReturn>(Func<T,TReturn> a)=>ToTask<T>().Then(a);
	public Task Catch(Action<Exception> a)=>Task.Catch(a);
	public Task Finally(Action a)=>Task.ContinueWith(_=>a());
	#endregion

	#region Cast
	public PendingCall<T> Cast<T>()=>this as PendingCall<T>??new PendingCall<T>(_rawData);
	public PendingCall Cast(Type t)=>new PendingCallCasted(_rawData,t);
	#endregion
}

[PublicAPI]
public class PendingCall<T>:PendingCall{
	internal PendingCall(PendingCallRawData other):base(other){
	}

	public static implicit operator Task<T>(PendingCall<T> call)=>call.ToTask();

	public Task<T> ToTask()=>ToTask<T>();
	public Task Then(Action<T> a)=>ToTask().Then(a);
	public Task<TReturn> Then<TReturn>(Func<T,TReturn> a)=>ToTask().Then(a);

	public new TaskAwaiter<T> GetAwaiter()=>ToTask().GetAwaiter();
	public new PendingCall<T> WithCancellation(CancellationToken token)=>(PendingCall<T>)base.WithCancellation(token);
}

[PublicAPI]
public class PendingCallCasted:PendingCall{
	private readonly Type _type;

	internal PendingCallCasted(PendingCallRawData other,Type type):base(other){
		_type=type;
	}

	public Task<object?> ToTask()=>ToTask(_type);
	public static implicit operator Task<object?>(PendingCallCasted call)=>call.ToTask();
}