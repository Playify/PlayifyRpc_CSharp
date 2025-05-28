using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public abstract partial class Invoker{
	private static readonly ThreadLocal<string?> MetaCallType=new();
	private static readonly Dictionary<string,RpcInvoker.MethodCandidate> MetaCache=new();

	protected internal Task<RpcDataPrimitive> Invoke(string? type,string? method,RpcDataPrimitive[] args,FunctionCallContext ctx){
		if(method!=null) return DynamicInvoke(type,method,args,ctx);

		var meta=args.Length<1?null:args[0].To<string>();

		MetaCallType.Value=type;

		//Meta calls, using null as method

		RpcInvoker.MethodCandidate? candidate;
		lock(MetaCache)
			if(!MetaCache.TryGetValue(meta??"",out candidate))
				MetaCache[meta??""]=
					candidate=RpcInvoker.MethodCandidate.Create(
						          ((Delegate)(meta switch{
								                     "M"=>GetMethods,
								                     "S"=>GetMethodSignaturesBase,
								                     "V"=>GetRpcVersion,
								                     _=>throw new RpcMetaMethodNotFoundException(type,meta),
							                     })).Method)??throw new RpcMetaMethodNotFoundException(type,meta);

		return RpcInvoker.InvokeThrow(this,[candidate],args.Skip(1).ToArray(),msg=>new RpcMetaMethodNotFoundException(type,meta,msg),ctx);
	}

	protected ValueTask<(string[] arguments,string returns)[]> GetMethodSignaturesBase(string? method,ProgrammingLanguage lang=ProgrammingLanguage.CSharp){
		if(method!=null) return GetMethodSignatures(MetaCallType.Value,method,lang);
		return new ValueTask<(string[] arguments,string returns)[]>([
			..RpcTypeStringifier.MethodSignatures(GetMethods,lang,"M"),
			..RpcTypeStringifier.MethodSignatures(GetMethodSignaturesBase,lang,"S"),
			..RpcTypeStringifier.MethodSignatures(GetRpcVersion,lang,"V"),
		]);
	}


	protected abstract Task<RpcDataPrimitive> DynamicInvoke(string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx);
	protected abstract ValueTask<string[]> GetMethods();

	protected virtual ValueTask<string> GetRpcVersion(){
		var version=Assembly.GetExecutingAssembly().GetName().Version;
		return new ValueTask<string>((version?.ToString(version.Revision==0?3:4)??"Unknown")+" C#");
	}

	protected abstract ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,ProgrammingLanguage lang);
}