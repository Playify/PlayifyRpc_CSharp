using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;

namespace PlayifyRpc.Internal;

[PublicAPI]
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

	public static bool CheckType(string type){
		lock(Types) return Types.ContainsKey(type);
	}

	public static string[] GetAllTypes(){
		lock(Types) return Types.Keys.OrderBy(s=>s).ToArray();
	}

	public static string[] GetAllConnections(){
		lock(ServerConnection.Connections) return ServerConnection.Connections.Select(c=>c.PrettyName).OrderBy(s=>s).ToArray();
	}

	public static StringMap<string[]> GetRegistrations(){
		lock(ServerConnection.Connections)
			return ServerConnection
			       .Connections
			       .ToDictionary(c=>c.PrettyName,c=>{
				       lock(Types) return c.Types.ToArray();
			       });
	}

	#region Clones from Rpc class
	public static Task<object?> CallFunction(string type,string method,params object?[] args){
		var ctx=Rpc.GetContext();
		var call=Rpc.CallFunction(type,method,args)
		            .WithCancellation(ctx.CancellationToken);
		call.AddMessageListener(ctx.SendMessage);
		ctx.AddMessageListener(call.SendMessage);

		return call;
	}

	public static Task<string> Eval(string expression,bool pretty=true)=>Evaluate.Eval(expression,pretty);
	public static Task<object?> EvalAny(string expression)=>Evaluate.EvalAny(expression);
	#endregion

	#region Extension Methods, not available via eval
	public static bool Exists(string type)=>CheckType(type);
	public static Task<string[]> GetMethods(string type)=>new RpcObject(type).GetMethods();
	#endregion

	#region Test functions
	public static void Void(params object?[] o){}

	public static object? Return(object? o)=>o;
	public static object?[] ReturnArguments(params object?[] o)=>o;
	public static void Throw(string? msg=null)=>throw new Exception(msg);
	#endregion
}