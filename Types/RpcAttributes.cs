using System.Reflection;
using JetBrains.Annotations;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types;

/**
 * Used to hide fields in RpcDataObjects or methods in registered types
 */
[PublicAPI]
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Method)]
public sealed class RpcHiddenAttribute:Attribute;

/**
 * Used to rename fields/methods to names that are normally not possible in C#
 */
[PublicAPI]
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Method)]
public sealed class RpcNamedAttribute(string name):Attribute{
	public readonly string Name=name;
}

/**
 * Used to register a static class simmilar to Rpc.RegisterType(name,typeof(MyClass))
 * if type name is not provided, then the class name is used.
 */
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
public sealed class RpcProviderAttribute(string? type=null):Attribute{
	internal readonly string? Type=type;
}

/**
 * Classes with this Attribute get initialized, as soon as Rpc initializes. (static constructor gets called)
 */
[AttributeUsage(AttributeTargets.Class)]
[MeansImplicitUse]
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