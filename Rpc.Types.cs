using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task RegisterInvoker(string type,Invoker invoker)=>RegisteredTypes.Register(type,invoker);
	
	public static Task RegisterType<T>(string type,T instance)=>RegisteredTypes.Register(type,TypeInvoker.Create(instance));
	
	public static Task UnregisterType(string type)=>RegisteredTypes.Unregister(type);
	
	
	
	public static async Task<int> CheckTypes(params string[] types)=>await CallFunction<int>(null,"?",types.Cast<object?>().ToArray());
	
	public static async Task<bool> CheckType(string type)=>await CallFunction<int>(null,"?",type)!=0;

	public static async Task<string[]> GetAllTypes()=>await CallFunction<string[]>(null,"T");

	public static async Task<string[]> GetAllConnections()=>await CallFunction<string[]>(null,"C");
}