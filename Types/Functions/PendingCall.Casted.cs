using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Functions;

[PublicAPI]
public class PendingCallCasted:PendingCall{
	private readonly Type _type;

	internal PendingCallCasted(PendingCallRawData rawData,Type type):base(rawData){
		_type=type;
	}

	public new PendingCallCasted WithCancellation(CancellationToken token)=>(PendingCallCasted)base.WithCancellation(token);
	public new PendingCallCasted SendMessage(params object?[] args)=>(PendingCallCasted)base.SendMessage(args);
	public new PendingCallCasted SendMessageRaw(RpcDataPrimitive[] args)=>(PendingCallCasted)base.SendMessageRaw(args);
	public new PendingCallCasted AddMessageListener(Delegate a)=>(PendingCallCasted)base.AddMessageListener(a);
	public new PendingCallCasted AddMessageListenerRaw(Action<RpcDataPrimitive[]> a)=>(PendingCallCasted)base.AddMessageListenerRaw(a);

	public PendingCall Void()=>new(RawData);

	public new Task<object?> ToTask()=>ToTask(_type);
	public static implicit operator Task<object?>(PendingCallCasted call)=>call.ToTask();
	public new TaskAwaiter<object?> GetAwaiter()=>ToTask().GetAwaiter();

	public Task Then(Action<object?> a)=>ToTask().Then(a);
	public Task<TReturn> Then<TReturn>(Func<object?,TReturn> a)=>ToTask().Then(a);
}