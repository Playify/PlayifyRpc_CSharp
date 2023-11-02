using System.Runtime.CompilerServices;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task RegisterType<T>(string type,T instance){
		if(instance is Invoker i) return RegisteredTypes.Register(type,i);
		if(instance==null&&typeof(T).IsAssignableTo(typeof(Invoker))){
			RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
			return RegisteredTypes.Register(type,(Invoker) Activator.CreateInstance(typeof(T))!);
		}
		return RegisteredTypes.Register(type,TypeInvoker.Create(instance));
	}

	public static Task UnregisterType(string type)=>RegisteredTypes.Unregister(type);
	
	
	
	public static async Task<int> CheckTypes(params string[] types)=>await CallFunction<int>(null,"?",types.Cast<object?>().ToArray());
	
	public static async Task<bool> CheckType(string type)=>await CallFunction<int>(null,"?",type)!=0;

	public static async Task<string[]> GetAllTypes()=>await CallFunction<string[]>(null,"T");

	public static async Task<string[]> GetAllConnections()=>await CallFunction<string[]>(null,"C");
}