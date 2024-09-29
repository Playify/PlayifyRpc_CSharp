using System.Reflection;
using System.Runtime.CompilerServices;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

internal static class RegisteredTypes{
	internal static readonly Dictionary<string,Invoker> Registered=new();

	internal static string? Name;

	static RegisteredTypes(){
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);

		typeof(RpcFunction).RunClassConstructor();//Let RpcFunction register its internal type
	}

	private static void RegisterAssembly(Assembly assembly){
		if(assembly.FullName?.StartsWith("System.")??false) return;//Skip System assemblies
		
		try{
			foreach(var type in assembly.GetTypes()){
				var sharedClass=type.GetCustomAttribute<RpcProviderAttribute>();
				if(sharedClass!=null){
					if(typeof(Invoker).IsAssignableFrom(type)){
						RuntimeHelpers.RunClassConstructor(type.TypeHandle);
						_=Register(sharedClass.Type??type.Name,(Invoker)Activator.CreateInstance(type)!);
					} else _=Register(sharedClass.Type??type.Name,new TypeInvoker(type));
				}
			}
		} catch(Exception e){
			Rpc.Logger.Critical("Error registering assembly \""+assembly+"\": "+e);
		}
	}

	internal static async Task Register(string type,Invoker invoker){
		lock(Registered)
			Registered.Add(type,invoker);
		try{
			if(Rpc.IsConnected) await FunctionCallContext.CallFunction(null,"+",type);
		} catch(Exception e){
			Rpc.Logger.Error($"Error registering type \"{type}\": {e}");

			lock(Registered)
				if(Registered.TryGetValue(type,out var revert)&&revert==invoker)
					Registered.Remove(type);
		}
	}

	internal static async Task Unregister(string type){
		lock(Registered)
			if(!Registered.ContainsKey(type))
				return;
		try{
			if(Rpc.IsConnected) await FunctionCallContext.CallFunction(null,"-",type);
		} catch(Exception e){
			Rpc.Logger.Error($"Error unregistering type \"{type}\": {e}");

			//Also delete locally, as it won't be listened to, and on the server it probably is already unregistered
		} finally{
			lock(Registered) Registered.Remove(type);
		}
	}

	internal static async Task SetName(string? name){
		Name=name;
		try{
			if(Rpc.IsConnected) await FunctionCallContext.CallFunction(null,"N",name);
		} catch(Exception e){
			Rpc.Logger.Error($"Error changing name to \"{name}\": {e}");
		}
	}
}