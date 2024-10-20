using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;

namespace PlayifyRpc.Types.Functions;

internal class PendingCallRawData{
	internal readonly TaskCompletionSource<RpcDataPrimitive> TaskCompletionSource=new();
	internal Action? CancelFunc;
	internal Action<RpcDataPrimitive[]>? SendFunc;
	internal readonly MessageQueue MessageQueue;

	public PendingCallRawData(){
		MessageQueue=new MessageQueue(TaskCompletionSource.Task);
	}

	public bool Finished=>TaskCompletionSource.Task.IsCompleted;
	public Task<RpcDataPrimitive> TaskRaw=>TaskCompletionSource.Task;

	internal void WithCancellation(CancellationToken token){
		if(Finished) return;
		var registration=token.Register(CancelFunc!);

#pragma warning disable CA2016
		// ReSharper disable once MethodSupportsCancellation
		TaskRaw.ContinueWith(_=>registration.Dispose());
#pragma warning restore CA2016
	}

	internal void Resolve(RpcDataPrimitive o)=>TaskCompletionSource.TrySetResult(o);
	internal void Reject(Exception e)=>TaskCompletionSource.TrySetException(e is RpcException rpc?rpc.Unfreeze():e);
}