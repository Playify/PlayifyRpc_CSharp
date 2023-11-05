using PlayifyRpc.Connections;

namespace PlayifyRpc.Internal;

public static class RpcServerTypes{//Class is registered as "Rpc" from Server
	
	internal static readonly Dictionary<string,ServerConnection> Types=new();

	
	public static int CheckTypes(IEnumerable<string> types){
		lock(Types) return Types.Keys.Intersect(types).Count();
	}
	public static bool HasType(string? type)=>CheckType(type);
	public static bool CheckType(string? type){
		if(type==null) return false;
		lock(Types) return Types.ContainsKey(type);
	}

	public static string[] GetAllTypes(){
		lock(Types) return Types.Keys.ToArray();
	}
	
	public static string[] GetAllConnections(){
		lock(ServerConnection.Connections) return ServerConnection.Connections.Select(c=>c.ToString()).ToArray();
	}
	
	public static Task<object?> CallFunction(string? type,string method,params object?[] args){
		var ctx=Rpc.GetContext();
		var call=Rpc.CallFunction(type,method,args)
		            .WithCancellation(ctx.CancellationToken);
		call.AddMessageListener(ctx.SendMessage);
		ctx.AddMessageListener(call.SendMessage);

		return call;
	}

	public static Task<string> Eval(string expression)=>Evaluate.Eval(expression);
}