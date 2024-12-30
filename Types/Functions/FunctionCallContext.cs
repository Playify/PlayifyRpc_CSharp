using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Functions;

public delegate void MessageFunc(params RpcDataPrimitive[] args);

[PublicAPI]
public sealed class FunctionCallContext{
	private readonly CancellationTokenSource _cts=new();
	private readonly MessageFunc _send;
	private readonly Func<Task<string>> _caller;
	private readonly TaskCompletionSource<RpcDataPrimitive> _tcs;
	internal readonly MessageQueue MessageQueue;

	public readonly string? Type;
	public readonly string? Method;

	internal FunctionCallContext(string? type,string? method,RpcDataPrimitive[]? args,MessageFunc send,TaskCompletionSource<RpcDataPrimitive> tcs,Func<Task<string>> caller){
		Type=type;
		Method=method;
		_send=send;
		_tcs=tcs;
		_caller=caller;
		MessageQueue=new MessageQueue(tcs.Task,type,method,args,_caller);
	}

	public Task<string> GetCaller()=>_caller();


	public void CancelSelf()=>_cts.Cancel();
	public void CancelSelfAfter(TimeSpan delay)=>_cts.CancelAfter(delay);
	public CancellationToken CancellationToken=>_cts.Token;

	public bool Finished=>_tcs.Task.IsCompleted;
	public Task<RpcDataPrimitive> TaskRaw=>_tcs.Task;
	

	public FunctionCallContext SendMessage(params object?[] args){
		var already=new RpcDataPrimitive.Already(a=>TaskRaw.ContinueWith(_=>a(),default(CancellationToken)));
		return SendMessageRaw(RpcDataPrimitive.FromArray(args,already));
	}

	public FunctionCallContext SendMessageRaw(RpcDataPrimitive[] args){
		_send(args);
		return this;
	}

	public FunctionCallContext AddMessageListener(Delegate a){
		MessageQueue.AddMessageListener(a);
		return this;
	}

	public FunctionCallContext AddMessageListenerRaw(Action<RpcDataPrimitive[]> a){
		MessageQueue.AddMessageListenerRaw(a);
		return this;
	}

	public IAsyncEnumerator<RpcDataPrimitive[]> GetAsyncEnumerator(CancellationToken cancel=default)=>MessageQueue.GetAsyncEnumerator(cancel);
}