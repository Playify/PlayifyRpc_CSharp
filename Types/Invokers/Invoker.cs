using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public abstract class Invoker{
	private static readonly ThreadLocal<string?> MetaCallType=new();

	protected internal object? Invoke(string? type,string? method,RpcDataPrimitive[] args){
		if(method!=null) return DynamicInvoke(type,method,args);

		var meta=args.Length<1?null:args[0].To<string>();

		MetaCallType.Value=type;
		//Meta calls, using null as method
		Delegate @delegate=meta switch{
			"M"=>GetMethods,
			"S"=>GetMethodSignaturesBase,
			"V"=>GetRpcVersion,
			_=>throw new RpcMetaMethodNotFoundException(type,meta),
		};
		return DynamicBinder.InvokeMeta(@delegate,type,meta,args.Skip(1).ToArray());
	}

	protected ValueTask<(string[] arguments,string returns)[]> GetMethodSignaturesBase(string? method,bool ts=false){
		if(method!=null) return GetMethodSignatures(MetaCallType.Value,method,ts);
		return new ValueTask<(string[] arguments,string returns)[]>([
			..RpcDataTypeStringifier.MethodSignatures(GetMethods,ts,"M"),
			..RpcDataTypeStringifier.MethodSignatures(GetMethodSignaturesBase,ts,"S"),
			..RpcDataTypeStringifier.MethodSignatures(GetRpcVersion,ts,"V"),
		]);
	}


	protected abstract object? DynamicInvoke(string? type,string method,RpcDataPrimitive[] args);
	protected abstract ValueTask<string[]> GetMethods();

	protected virtual ValueTask<string> GetRpcVersion(){
		var version=Assembly.GetExecutingAssembly().GetName().Version;
		return new ValueTask<string>((version?.ToString(version.Revision==0?3:4)??"Unknown")+" C#");
	}

	protected abstract ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,bool ts);

	protected internal PendingCall<RpcDataPrimitive> Call(string type,string? method,RpcDataPrimitive[] args)=>CallLocal(()=>Invoke(type,method,args),type,method,args);
	protected internal PendingCall<T> Call<T>(string type,string method,RpcDataPrimitive[] args)=>Call(type,method,args).Cast<T>();

	internal static PendingCall CallLocal(Action a)
		=>CallLocal(()=>{
			try{
				a();
			} catch(Exception e){
				throw RpcException.WrapAndFreeze(e).Remove(MethodBase.GetCurrentMethod());
			}
			return null;
		});

	internal static PendingCall<RpcDataPrimitive> CallLocal(Func<object?> a)=>CallLocal(a,null,null,null);
	internal static PendingCall<T> CallLocal<T>(Func<object?> a)=>CallLocal(a).Cast<T>();

	private static PendingCall<RpcDataPrimitive> CallLocal(Func<object?> a,string? type,string? method,RpcDataPrimitive[]? args){
		var rawData=new PendingCallRawData();
		var context=new FunctionCallContext(type,
			method,
			sending=>Task.Run(()=>rawData.MessageQueue.DoReceiveMessage(sending)).Catch(e=>Rpc.Logger.Warning("Error while handling message: "+e)),
			rawData.TaskCompletionSource,
			()=>Task.FromResult(Rpc.PrettyName));

		rawData.SendFunc=received=>{
			if(!rawData.Finished) Task.Run(()=>context.MessageQueue.DoReceiveMessage(received));
		};
		rawData.CancelFunc=()=>context.CancelSelf();


		var call=new PendingCall<RpcDataPrimitive>(rawData);

		var task=FunctionCallContext.RunWithContextAsync(a,context,type,method,args);
		TaskToCall(task,rawData).Background();
		return call;
	}

	private static async Task TaskToCall(Task<object?> task,PendingCallRawData rawData){
		try{
			rawData.Resolve(RpcDataPrimitive.From(await task));
		} catch(Exception e){
			rawData.Reject(e);
		}
	}
}