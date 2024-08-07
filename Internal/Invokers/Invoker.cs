using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public abstract class Invoker{
	protected internal object? Invoke(string? type,string? method,object?[] args){
		if(method!=null) return DynamicInvoke(type,method,args);

		var meta=args.Length==0?null:DynamicCaster.Cast<string>(args[0]);

		//Meta calls, using null as method
		return meta switch{
			"M"=>GetMethods().Push(out var valueTask).IsCompletedSuccessfully?valueTask.Result:valueTask.AsTask(),
			_=>throw new RpcMetaMethodNotFoundException(type,meta),
		};
	}

	protected abstract object? DynamicInvoke(string? type,string method,object?[] args);
	protected abstract ValueTask<string[]> GetMethods();


	public PendingCall Call(string type,string? method,object?[] args)=>CallLocal(()=>Invoke(type,method,args),type,method,args);
	public PendingCall<T> Call<T>(string type,string method,object?[] args)=>Call(type,method,args).Cast<T>();

	internal static PendingCall CallLocal(Action a)
		=>CallLocal(()=>{
			try{
				a();
			} catch(Exception e){
				throw RpcException.WrapAndFreeze(e).Remove(MethodBase.GetCurrentMethod());
			}
			return null;
		});

	internal static PendingCall CallLocal(Func<object?> a)=>CallLocal(a,null,null,null);
	internal static PendingCall<T> CallLocal<T>(Func<object?> a)=>CallLocal(a).Cast<T>();

	private static PendingCall CallLocal(Func<object?> a,string? type,string? method,object?[]? args){
		var truth=new PendingCallRawData();
		var context=new FunctionCallContext(type,
			method,
		                                    sending=>Task.Run(()=>truth.DoReceiveMessage(sending)).Catch(e=>Rpc.Logger.Warning("Error while handling message: "+e)),
			truth.TaskCompletionSource,
			()=>Task.FromResult(Rpc.PrettyName));

		truth.SendFunc=received=>Task.Run(()=>context.DoReceiveMessage(received));
		truth.CancelFunc=()=>context.CancelSelf();


		var call=new PendingCall<object?>(truth);

		var task=FunctionCallContext.RunWithContextAsync(a,context,type,method,args);
		TaskToCall(task,call).Background();
		return call;
	}

	internal static async Task TaskToCall(Task<object?> task,PendingCall call){
		try{
			call.Resolve(await task);
		} catch(Exception e){
			call.Reject(e);
		}
	}
}