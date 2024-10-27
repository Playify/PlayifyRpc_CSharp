using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public abstract partial class Invoker{
	private static readonly ThreadLocal<string?> MetaCallType=new();

	protected internal object? Invoke(string? type,string? method,RpcDataPrimitive[] args,FunctionCallContext ctx){
		if(method!=null) return DynamicInvoke(type,method,args,ctx);

		var meta=args.Length<1?null:args[0].To<string>();

		MetaCallType.Value=type;
		//Meta calls, using null as method
		Delegate @delegate=meta switch{
			"M"=>GetMethods,
			"S"=>GetMethodSignaturesBase,
			"V"=>GetRpcVersion,
			_=>throw new RpcMetaMethodNotFoundException(type,meta),
		};
		return DynamicBinder.InvokeMeta(@delegate,type,meta,args.Skip(1).ToArray(),ctx);
	}

	protected ValueTask<(string[] arguments,string returns)[]> GetMethodSignaturesBase(string? method,bool ts=false){
		if(method!=null) return GetMethodSignatures(MetaCallType.Value,method,ts);
		return new ValueTask<(string[] arguments,string returns)[]>([
			..RpcDataTypeStringifier.MethodSignatures(GetMethods,ts,"M"),
			..RpcDataTypeStringifier.MethodSignatures(GetMethodSignaturesBase,ts,"S"),
			..RpcDataTypeStringifier.MethodSignatures(GetRpcVersion,ts,"V"),
		]);
	}


	protected abstract object? DynamicInvoke(string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx);
	protected abstract ValueTask<string[]> GetMethods();

	protected virtual ValueTask<string> GetRpcVersion(){
		var version=Assembly.GetExecutingAssembly().GetName().Version;
		return new ValueTask<string>((version?.ToString(version.Revision==0?3:4)??"Unknown")+" C#");
	}

	protected abstract ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,bool ts);
}