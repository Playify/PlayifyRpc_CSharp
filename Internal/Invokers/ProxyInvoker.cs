using JetBrains.Annotations;
using PlayifyRpc.Types;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class ProxyInvoker:Invoker{
	private readonly Func<Task<RpcObject>> _object;

	public ProxyInvoker(Func<Task<RpcObject>> @object)=>_object=@object;
	public ProxyInvoker(Func<RpcObject> @object)=>_object=()=>Task.Run(@object);

	protected internal override object DynamicInvoke(string? type,string method,object?[] args){
		var ctx=Rpc.GetContext();
		return _object().ThenAsync(o=>{
			var call=o.CallFunction(method,args)
			          .WithCancellation(ctx.CancellationToken);
			call.AddMessageListener(ctx.SendMessage);
			ctx.AddMessageListener(call.SendMessage);

			return call;
		});
	}
}