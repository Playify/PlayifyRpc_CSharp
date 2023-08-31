using System.Runtime.CompilerServices;
using PlayifyRpc.Internal;
using PlayifyRpc.Types.Data;
using PlayifyUtils.Utils;

namespace PlayifyRpc.Types.Functions;

internal class PendingCallRawData:SendReceive{
	internal readonly TaskCompletionSource<object?> TaskCompletionSource=new();
	internal MessageFunc? SendFunc;
	internal Action? CancelFunc;

	public override void SendMessage(params object?[] args){
		if(Finished) return;
		SendFunc?.Invoke(args);
	}

	public override bool Finished=>TaskCompletionSource.Task.IsCompleted;
	public override Task<object?> Task=>TaskCompletionSource.Task;

	public void SendCancel(){
		if(Finished) return;
		CancelFunc?.Invoke();
	}
}

public class PendingCall:SendReceive{

	#region Internal
	private readonly PendingCallRawData _rawData;

	internal PendingCall(PendingCallRawData rawData)=>_rawData=rawData;
	internal PendingCall(PendingCall other)=>_rawData=other._rawData;

	internal void Resolve(object? o)=>_rawData.TaskCompletionSource.TrySetResult(o);
	internal void Reject(Exception e)=>_rawData.TaskCompletionSource.TrySetException(e);
	#endregion

	#region SendReceive
	public override bool Finished=>_rawData.Finished;
	public override Task<object?> Task=>_rawData.Task;

	public override void SendMessage(params object?[] args)=>_rawData.SendMessage(args);

	public override void AddMessageListener(MessageFunc a)=>_rawData.AddMessageListener(a);

	internal override void DoReceiveMessage(object?[] args)=>_rawData.DoReceiveMessage(args);
	#endregion

	public void Cancel()=>_rawData.SendCancel();

	public PendingCall WithCancellation(CancellationToken token){
		if(Finished) return this;
		var registration=token.Register(_rawData.SendCancel);
		Task.Then(registration.Unregister);

		return this;
	}

	#region Task
	protected virtual Task<object?> AsTask()=>_rawData.TaskCompletionSource.Task;
	public static implicit operator Task<object?>(PendingCall call)=>call.AsTask();
	public static implicit operator Task(PendingCall call)=>(Task<object?>)call;
	public TaskAwaiter<object?> GetAwaiter()=>((Task<object?>)this).GetAwaiter();


	public Task Then(Action<object?> a)=>((Task<object?>)this).Then(a);
	public Task<T> Then<T>(Func<object?,T> a)=>((Task<object?>)this).Then(a);
	public Task Catch(Action<Exception> a)=>((Task<object?>)this).Catch(a);
	public Task Finally(Action a)=>((Task<object?>)this).Finally(a);
	#endregion

	#region Cast
	public PendingCall<T> Cast<T>()=>new(this);
	public PendingCall Cast(Type t)=>new PendingCallCasted(this,t);
	#endregion

}

public class PendingCall<T>:PendingCall{
	internal PendingCall(PendingCall other):base(other){
	}

	public static implicit operator Task<T>(PendingCall<T> call)=>call.Then(StaticallyTypedUtils.DoCast<T>);
	public new TaskAwaiter<T> GetAwaiter()=>((Task<T>)this).GetAwaiter();
	public new PendingCall<T> WithCancellation(CancellationToken token)=>(PendingCall<T>)base.WithCancellation(token);
}

public class PendingCallCasted:PendingCall{
	private readonly Type _type;

	internal PendingCallCasted(PendingCall other,Type type):base(other)=>_type=type;

	protected override Task<object?> AsTask()=>base.AsTask().Then(o=>StaticallyTypedUtils.DoCast(o,_type))!;
}