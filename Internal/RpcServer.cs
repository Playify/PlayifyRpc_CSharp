using PlayifyRpc.Connections;
using PlayifyRpc.Types;

namespace PlayifyRpc.Internal;

public static class RpcServer{//Class is registered as "Rpc" from Server

	internal static readonly Dictionary<string,ServerConnection> Types=new();

	public static RpcObject? GetObjectWithFallback(params string[] types){
		lock(Types)
			foreach(var s in types)
				if(Types.ContainsKey(s))
					return new RpcObject(s);
		return null;
	}

	public static int CheckTypes(params string[] types){
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
	public static Task<object?> EvalAny(string expression)=>Evaluate.EvalAny(expression);
}