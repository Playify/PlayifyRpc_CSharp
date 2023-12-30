using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Functions;

internal class PendingCallRawData:SendReceive{
	internal readonly TaskCompletionSource<object?> TaskCompletionSource=new();
	internal Action? CancelFunc;
	internal MessageFunc? SendFunc;

	public override bool Finished=>TaskCompletionSource.Task.IsCompleted;
	public override Task<object?> Task=>TaskCompletionSource.Task;

	public override void SendMessage(params object?[] args){
		if(Finished) return;
		SendFunc?.Invoke(args);
	}

	public void SendCancel(){
		if(Finished) return;
		CancelFunc?.Invoke();
	}
}

[PublicAPI]
public class PendingCall:SendReceive{
	public void Cancel()=>_rawData.SendCancel();

	public PendingCall WithCancellation(CancellationToken token){
		if(Finished) return this;
		var registration=token.Register(_rawData.SendCancel);
		Task.Then(registration.Dispose);

		return this;
	}

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

	#region Task
	protected virtual Task<object?> AsTask()=>_rawData.TaskCompletionSource.Task;
	public static implicit operator Task<object?>(PendingCall call)=>call.AsTask();
	public static implicit operator Task(PendingCall call)=>(Task<object?>)call;
	public TaskAwaiter<object?> GetAwaiter()=>((Task<object?>)this).GetAwaiter();

	public Task<T> ToTask<T>()=>AsTask().Then(StaticallyTypedUtils.Cast<T>);

	public Task Then(Action<object?> a)=>((Task<object?>)this).Then(a);
	public Task<T> Then<T>(Func<object?,T> a)=>((Task<object?>)this).Then(a);
	public Task Catch(Action<Exception> a)=>((Task<object?>)this).Catch(a);
	public Task Finally(Action a)=>((Task<object?>)this).Finally(a);
	#endregion

	#region Cast
	public PendingCall<T> Cast<T>()=>this as PendingCall<T>??new PendingCall<T>(this);
	public PendingCall Cast(Type t)=>new PendingCallCasted(this,t);
	#endregion
}

[PublicAPI]
public class PendingCall<T>:PendingCall{
	internal PendingCall(PendingCall other):base(other){
	}

	protected override Task<object?> AsTask()=>base.AsTask().Then(o=>StaticallyTypedUtils.Cast(o,typeof(T)))!;

	public static implicit operator Task<T>(PendingCall<T> call)=>call.ToTask();

	public Task<T> ToTask()=>Then(StaticallyTypedUtils.Cast<T>);

	public new TaskAwaiter<T> GetAwaiter()=>((Task<T>)this).GetAwaiter();
	public new PendingCall<T> WithCancellation(CancellationToken token)=>(PendingCall<T>)base.WithCancellation(token);
}

[PublicAPI]
public class PendingCallCasted:PendingCall{
	private readonly Type _type;

	internal PendingCallCasted(PendingCall other,Type type):base(other)=>_type=type;

	protected override Task<object?> AsTask()=>base.AsTask().Then(o=>StaticallyTypedUtils.Cast(o,_type))!;
}