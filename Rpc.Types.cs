using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task RegisterType(string type,object instance)
		=>instance is Type t?RegisterType(type,t):RegisterType(type,instance as Invoker??new TypeInvoker(instance));

	public static Task RegisterType(string type,Invoker invoker)=>RegisteredTypes.Register(type,invoker);

	public static Task RegisterType(string type,Type t){
		if(!typeof(Invoker).IsAssignableFrom(t)) return RegisteredTypes.Register(type,new TypeInvoker(t));
		t.RunClassConstructor();
		return RegisteredTypes.Register(type,(Invoker)Activator.CreateInstance(t)!);
	}

	public static Task RegisterType<T>(string type)=>RegisterType(type,typeof(T));

	public static Task RegisterType(out string type,object instance){
		type=GenerateTypeName();
		return RegisterType(type,instance);
	}


	public static Task UnregisterType(string type)=>RegisteredTypes.Unregister(type);
	public static Task UnregisterType(RpcObject type)=>RegisteredTypes.Unregister(type.Type);
	public static string GenerateTypeName()=>"$"+Id+"$"+Guid.NewGuid().ToString("N");


	public static async Task<RpcObject?> GetObjectWithFallback(string type,params string[] fallback)//
		=>await CallFunction<RpcObject>("Rpc","GetObjectWithFallback",fallback.Prepend(type).Cast<object?>().ToArray());

	public static async Task<int> CheckTypes(params string[] types)=>await CallFunction<int>("Rpc","CheckTypes",types.Cast<object?>().ToArray());

	public static async Task<bool> CheckType(string type)=>await CallFunction<bool>("Rpc","CheckType",type);

	public static async Task<string[]> GetAllTypes()=>await CallFunction<string[]>("Rpc","GetAllTypes");

	public static async Task<string[]> GetAllConnections()=>await CallFunction<string[]>("Rpc","GetAllConnections");

	public static async Task<Dictionary<string,string[]>> GetRegistrations(bool includeHidden=false)=>await CallFunction<StringMap<string[]>>("Rpc","GetRegistrations",includeHidden);

	public static PendingCall ListenCalls()=>CallFunction("Rpc","ListenCalls");
}