using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;

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

	protected override object DynamicInvoke(string? type,string method,RpcDataPrimitive[] args)=>InvokeAsync(type,method,args);

	private async Task<object?> InvokeAsync(string? type,string method,RpcDataPrimitive[] args){
		var ctx=Rpc.GetContext();

		try{
			var o=await _object();
			var call=o.CallFunctionRaw(method,args)
			          .WithCancellation(ctx.CancellationToken);
			_=call.AddMessageListenerRaw(msg=>ctx.SendMessageRaw(msg));
			_=ctx.AddMessageListenerRaw(msg=>call.SendMessageRaw(msg));

			return await call;
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e).Remove(((Delegate)InvokeAsync).Method);
		}
	}

	protected override async ValueTask<string[]> GetMethods()=>await (await _object()).GetMethods();
	protected override async ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,bool ts)=>await (await _object()).GetMethodSignatures(method,ts);
}