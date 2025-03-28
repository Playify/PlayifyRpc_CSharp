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
		return RpcInvoker.InvokeMeta(@delegate,type,meta,args.Skip(1).ToArray(),ctx);
	}

	protected ValueTask<(string[] arguments,string returns)[]> GetMethodSignaturesBase(string? method,ProgrammingLanguage lang=ProgrammingLanguage.CSharp){
		if(method!=null) return GetMethodSignatures(MetaCallType.Value,method,lang);
		return new ValueTask<(string[] arguments,string returns)[]>([
			..RpcTypeStringifier.MethodSignatures(GetMethods,lang,"M"),
			..RpcTypeStringifier.MethodSignatures(GetMethodSignaturesBase,lang,"S"),
			..RpcTypeStringifier.MethodSignatures(GetRpcVersion,lang,"V"),
		]);
	}


	protected abstract object? DynamicInvoke(string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx);
	protected abstract ValueTask<string[]> GetMethods();

	protected virtual ValueTask<string> GetRpcVersion(){
		var version=Assembly.GetExecutingAssembly().GetName().Version;
		return new ValueTask<string>((version?.ToString(version.Revision==0?3:4)??"Unknown")+" C#");
	}

	protected abstract ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,ProgrammingLanguage lang);
}