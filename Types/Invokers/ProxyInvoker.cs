using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public class ProxyInvoker:Invoker{
	private readonly Func<Task<RpcObject>> _object;

	public ProxyInvoker(Func<Task<RpcObject>> @object)=>_object=@object;
	public ProxyInvoker(Func<RpcObject> @object)=>_object=()=>Task.Run(@object);

	public ProxyInvoker(RpcObject @object){
		var task=Task.FromResult(@object);
		_object=()=>task;
	}

	protected override async Task<RpcDataPrimitive> DynamicInvoke(string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx){
		try{
			return await (await _object()).CallFunctionRaw(method,args).AsForwarded(ctx);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e);
		}
	}

	protected override async ValueTask<string[]> GetMethods()=>await (await _object()).GetMethods();
	protected override async ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,ProgrammingLanguage lang)=>await (await _object()).GetFunction(method).GetSignatures(lang);
}