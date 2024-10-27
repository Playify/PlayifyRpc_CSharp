using System.Reflection;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[AttributeUsage(AttributeTargets.Class)]
public class RpcSetupAttribute:Attribute{
	private static bool _loaded;

	internal static void LoadAll(){
		if(_loaded) return;
		_loaded=true;
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);
	}

	private static void RegisterAssembly(Assembly assembly){
		// ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
		if(assembly.FullName?.StartsWith("System.")??false) return;//Skip System assemblies

		try{
			foreach(var type in assembly.GetTypes())
				if(type.IsDefined(typeof(RpcSetupAttribute),true))
					type.RunClassConstructor();
		} catch(Exception e){
			Rpc.Logger.Critical("Error registering assembly \""+assembly+"\": "+e);
		}
	}
}