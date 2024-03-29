using JetBrains.Annotations;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public abstract class Invoker{
	protected internal object? Invoke(string? type,string? method,object?[] args){
		if(method!=null) return DynamicInvoke(type,method,args);
		return (args.Length==0?null:args[0]) switch{
			"M"=>GetMethods().Push(out var valueTask).IsCompletedSuccessfully?valueTask.Result:valueTask.AsTask(),
			_=>throw new ArgumentException("Invalid meta-call"),
		};
	}

	protected abstract object? DynamicInvoke(string? type,string method,object?[] args);
	protected abstract ValueTask<string[]> GetMethods();


	public PendingCall Call(string type,string? method,object?[] args)=>CallLocal(type,method,()=>Invoke(type,method,args));
	public PendingCall<T> Call<T>(string type,string method,object?[] args)=>Call(type,method,args).Cast<T>();

	public static PendingCall CallLocal(Func<object?> a)=>CallLocal(null,null,a);
	public static PendingCall<T> CallLocal<T>(Func<object?> a)=>CallLocal(a).Cast<T>();

	private static PendingCall CallLocal(string? type,string? method,Func<object?> a){
		var truth=new PendingCallRawData();
		var context=new FunctionCallContext(type,
		                                    method,
		                                    sending=>Task.Run(()=>truth.DoReceiveMessage(sending)).Catch(e=>Console.WriteLine(e.ToString())),
		                                    truth.TaskCompletionSource);

		truth.SendFunc=received=>Task.Run(()=>context.DoReceiveMessage(received));
		truth.CancelFunc=()=>context.CancelSelf();


		var call=new PendingCall<object?>(truth);

		try{
			var invokeResult=FunctionCallContext.RunWithContext(a,context);

			StaticallyTypedUtils.UnwrapTask(invokeResult)
			                    .Then(res=>call.Resolve(res))
			                    .Catch(e=>call.Reject(e));
		} catch(Exception e){
			call.Reject(e);
		}
		return call;
	}
}