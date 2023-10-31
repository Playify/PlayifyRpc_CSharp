using PlayifyRpc.Types;

namespace PlayifyRpc.Internal.Invokers;

public class ProxyInvoker:Invoker{
	private readonly Func<RpcObject> _object;

	public ProxyInvoker(Func<RpcObject> @object){
		_object=@object;

	}

	protected internal override object DynamicInvoke(string? type,string method,object?[] args){
		var ctx=Rpc.GetContext();
		var o=_object();

		var call=o.CallFunction(method,args)
		          .WithCancellation(ctx.CancellationToken);
		call.AddMessageListener(ctx.SendMessage);
		ctx.AddMessageListener(call.SendMessage);
		
		return call;
	}
}