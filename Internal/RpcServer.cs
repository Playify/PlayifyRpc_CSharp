using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Utils.Extensions;

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

	public static async Task<StringMap<string>> GetConnectionVersions(){
		Task<(string PrettyName,string version)[]> task;
		lock(ServerConnection.Connections)
			task=Task.WhenAll(ServerConnection.Connections.Select(async c=>{
				try{
					return (c.PrettyName,await new RpcObject("$"+c.Id).GetRpcVersion());
				} catch(RpcMetaMethodNotFoundException){
					return (c.PrettyName,"Old Version");
				} catch(RpcException e){
					return (c.PrettyName,"Error:"+e);
				}
			}));
		return new StringMap<string>((await task).OrderBy(t=>t.PrettyName).ToDictionary());
	}

	public static async Task<StringMap<string[]>> GetUsedVersions(){
		var versions=await GetConnectionVersions();

		var map=new StringMap<string[]>();
		foreach(var grouping in versions.ToLookup(kv=>kv.Value,kv=>kv.Key).OrderBy(g=>g.Key))
			map.Add(grouping.Key,grouping.ToArray());
		return map;
	}

	public static StringMap<string[]> GetRegistrations(bool includeHidden=false){
		lock(ServerConnection.Connections){
			var map=new StringMap<string[]>();
			foreach(var c in ServerConnection
			                 .Connections
			                 .OrderBy(c=>c.PrettyName))
				lock(Types)
					map.Add(c.PrettyName,(includeHidden
						                      ?c.Types
						                      :c.Types.Where(t=>t!="$"+c.Id))
					                     .OrderBy(s=>s)
					                     .ToArray());
			return map;
		}
	}

	#region Clones from Rpc class
	public static Task<RpcDataPrimitive> CallFunction(FunctionCallContext ctx,string type,string method,params RpcDataPrimitive[] args){
		if(type==null) throw new NullReferenceException("Type is null");
		var call=Invoker.CallFunctionRaw(type,method,args).WithCancellation(ctx.CancellationToken);
		call.AddMessageListenerRaw(msg=>ctx.SendMessageRaw(msg));
		ctx.AddMessageListenerRaw(msg=>call.SendMessageRaw(msg));

		return call.TaskRaw;
	}

	public static Task<string> EvalString(string expression,bool pretty=true)=>Evaluate.EvalString(expression,pretty);
	public static Task<RpcDataPrimitive> EvalObject(string expression)=>Evaluate.EvalObject(expression);
	public static Task ListenCalls(FunctionCallContext ctx)=>ListenAllCalls.Listen(ctx);
	#endregion

	#region Extension Methods (otherwise only available in eval using specialized syntax)
	public static bool Exists(string type)=>CheckType(type);
	public static Task<string[]> GetMethods(string type)=>new RpcObject(type).GetMethods();
	public static Task<(string[] parameters,string returns)[]> GetMethodSignatures(string type,string method,bool typescript=false)=>new RpcFunction(type,method).GetMethodSignatures(typescript);
	#endregion

	#region Test functions
	public static void Void(params object?[] o){}

	public static object? Return(object? o)=>o;
	public static object?[] ReturnArguments(params object?[] o)=>o;
	public static void Throw(string? msg=null)=>throw new Exception(msg);
	#endregion

}