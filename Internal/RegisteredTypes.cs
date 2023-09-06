using System.Reflection;
using System.Runtime.CompilerServices;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types;

namespace PlayifyRpc.Internal;

internal static class RegisteredTypes{
	internal static readonly Dictionary<string,Invoker> Registered=new();

	static RegisteredTypes(){
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);

		RuntimeHelpers.RunClassConstructor(typeof(RpcFunction).TypeHandle);//initialize static constructor of RemoteFunction
	}

	private static void RegisterAssembly(Assembly assembly){
		foreach(var type in assembly.GetTypes()){
			var sharedClass=type.GetCustomAttribute<RpcProviderAttribute>();
			if(sharedClass!=null) _=Register(sharedClass.Type??type.Name,new TypeInvoker(type));
		}
	}

	internal static async Task Register(string type,Invoker invoker){
		lock(Registered)
			if(Registered.ContainsKey(type)) return;
			else Registered.Add(type,invoker);
		try{
			if(Rpc.IsConnected) await Rpc.CallFunction(null,"+",type);
		} catch(Exception e){
			Console.WriteLine(e);
		}
	}

	internal static async Task Unregister(string type){
		lock(Registered)
			if(!Registered.ContainsKey(type))
				return;
		try{
			if(Rpc.IsConnected) await Rpc.CallFunction(null,"-",type);
		} catch(Exception e){
			Console.WriteLine(e);
		} finally{
			lock(Registered) Registered.Remove(type);
		}
	}

	internal static string? Name;
	internal static async Task SetName(string? name){
		Name=name;
		try{
			if(Rpc.IsConnected) await Rpc.CallFunction(null,"N",Rpc.NameOrId);
		} catch(Exception e){
			Console.WriteLine(e);
		}
	}

}