using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task Register(string type,Invoker invoker)=>RegisteredTypes.Register(type,invoker);
	
	public static Task RegisterType<T>(string type,T instance)=>RegisteredTypes.Register(type,TypeInvoker.Create(instance));
	
	public static Task Unregister(string type)=>RegisteredTypes.Unregister(type);
	
	
	
	public static Task<int> CheckTypes(params string[] types)=>CallFunction<int>(null,"?",types.Cast<object?>().ToArray());
	
	public static async Task<bool> CheckType(string type)=>await CallFunction<int>(null,"?",type)!=0;

	public static Task<string[]> GetAllTypes()=>CallFunction<string[]>(null,"T");

	public static Task<string[]> GetAllConnections()=>CallFunction<string[]>(null,"C");
}