using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Functions;

[PublicAPI]
public class PendingCall<T>:PendingCall{
	internal PendingCall(PendingCallRawData rawData):base(rawData){}

	public new PendingCall<T> WithCancellation(CancellationToken token)=>(PendingCall<T>)base.WithCancellation(token);
	public new PendingCall<T> SendMessage(params object?[] args)=>(PendingCall<T>)base.SendMessage(args);
	public new PendingCall<T> SendMessageRaw(RpcDataPrimitive[] args)=>(PendingCall<T>)base.SendMessageRaw(args);
	public new PendingCall<T> AddMessageListener(Delegate a)=>(PendingCall<T>)base.AddMessageListener(a);
	public new PendingCall<T> AddMessageListenerRaw(Action<RpcDataPrimitive[]> a)=>(PendingCall<T>)base.AddMessageListenerRaw(a);

	public PendingCall Void()=>new(RawData);

	public new Task<T> ToTask()=>ToTask<T>()!;
	public static implicit operator Task<T>(PendingCall<T> call)=>call.ToTask();
	public new TaskAwaiter<T> GetAwaiter()=>ToTask().GetAwaiter();


	public Task Then(Action<T> a)=>ToTask().Then(a);
	public Task<TReturn> Then<TReturn>(Func<T,TReturn> a)=>ToTask().Then(a);
	public Task<T> Catch(Func<Exception,T> a)=>ToTask().Catch(a);
	public new Task<T> Finally(Action a)=>ToTask().Finally(a);
}