using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public abstract partial class Invoker{
	protected internal object? Invoke(string? type,string? method,object?[] args){
		if(method!=null) return DynamicInvoke(type,method,args);

		var meta=args.Length<1?null:DynamicCaster.Cast<string>(args[0]);

		//Meta calls, using null as method
		Delegate @delegate=meta switch{
			"M"=>GetMethods,
			"A"=>GetMethodSignaturesBase,
			_=>throw new RpcMetaMethodNotFoundException(type,meta),
		};
		return InvokeMeta(@delegate,type,meta,args.Skip(1).ToArray());
	}

	protected abstract object? DynamicInvoke(string? type,string method,object?[] args);
	protected abstract ValueTask<string[]> GetMethods();

	protected ValueTask<(string[] arguments,string @return)[]> GetMethodSignaturesBase(string? method,bool ts){
		if(method!=null) return GetMethodSignatures(method,ts);
		return new ValueTask<(string[] arguments,string @return)[]>([
			DynamicTypeStringifier.MethodSignature(GetMethods,ts,"M"),
			DynamicTypeStringifier.MethodSignature(GetMethodSignaturesBase,ts,"A"),
		]);
	}

	protected abstract ValueTask<(string[] parameters,string @return)[]> GetMethodSignatures(string method,bool ts);


	protected internal PendingCall Call(string type,string? method,object?[] args)=>CallLocal(()=>Invoke(type,method,args),type,method,args);
	protected internal PendingCall<T> Call<T>(string type,string method,object?[] args)=>Call(type,method,args).Cast<T>();

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

	private static async Task TaskToCall(Task<object?> task,PendingCall call){
		try{
			call.Resolve(await task);
		} catch(Exception e){
			call.Reject(e);
		}
	}
}