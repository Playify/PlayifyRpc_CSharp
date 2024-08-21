using JetBrains.Annotations;
using PlayifyRpc.Types;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class ProxyInvoker:Invoker{
	private readonly Func<Task<RpcObject>> _object;

	public ProxyInvoker(Func<Task<RpcObject>> @object)=>_object=@object;
	public ProxyInvoker(Func<RpcObject> @object)=>_object=()=>Task.Run(@object);
	public ProxyInvoker(RpcObject @object)=>_object=()=>Task.FromResult(@object);

	protected override object DynamicInvoke(string? type,string method,object?[] args)=>InvokeAsync(type,method,args);

	private async Task<object?> InvokeAsync(string? type,string method,object?[] args){
		var ctx=Rpc.GetContext();

		try{
			var o=await _object();
			var call=o.CallFunction(method,args)
			          .WithCancellation(ctx.CancellationToken);
			call.AddMessageListener(ctx.SendMessage);
			ctx.AddMessageListener(call.SendMessage);

			return await call;
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e).Remove(((Delegate)InvokeAsync).Method);
		}
	}

	protected override async ValueTask<string[]> GetMethods()=>await (await _object()).GetMethods();
	protected override async ValueTask<(string[] parameters,string @return)[]> GetMethodSignatures(string method,bool ts)=>await (await _object()).GetMethodSignatures(method,ts);
}