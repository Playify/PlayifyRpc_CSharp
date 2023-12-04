using System.Runtime.CompilerServices;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task RegisterType<T>(string type,T instance)
		=>instance switch{
			Invoker i=>RegisterType(type,i),
			Type t=>RegisterType(type,t),
			null=>RegisterType<T>(type),
			_=>RegisteredTypes.Register(type,TypeInvoker.Create(instance)),
		};

	public static Task RegisterType(string type,Invoker invoker)=>RegisteredTypes.Register(type,invoker);

	public static Task RegisterType(string type,Type t){
		if(!typeof(Invoker).IsAssignableFrom(t)) return RegisteredTypes.Register(type,new TypeInvoker(t));
		RuntimeHelpers.RunClassConstructor(t.TypeHandle);
		return RegisteredTypes.Register(type,(Invoker)Activator.CreateInstance(t)!);
	}

	public static Task RegisterType<T>(string type)=>RegisterType(type,typeof(T));

	public static Task UnregisterType(string type)=>RegisteredTypes.Unregister(type);


	public static async Task<RpcObject?> GetObjectWithFallback(string type,params string[] fallback)=>await CallFunction<RpcObject>(null,"O",fallback.Prepend(type).Cast<object?>().ToArray());

	public static async Task<int> CheckTypes(params string[] types)=>await CallFunction<int>(null,"?",types.Cast<object?>().ToArray());

	public static async Task<bool> CheckType(string type)=>await CallFunction<int>(null,"?",type)!=0;

	public static async Task<string[]> GetAllTypes()=>await CallFunction<string[]>(null,"T");

	public static async Task<string[]> GetAllConnections()=>await CallFunction<string[]>(null,"C");
}