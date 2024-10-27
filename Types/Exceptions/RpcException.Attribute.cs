using System.Reflection;

namespace PlayifyRpc.Types.Exceptions;

public partial class RpcException{
	private static readonly Dictionary<string,ConstructorInfo> Constructors=new();

	static RpcException(){
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);
	}

	private static void RegisterAssembly(Assembly assembly){
		// ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
		if(assembly.FullName?.StartsWith("System.")??false) return;//Skip System assemblies
		
		try{
			foreach(var type in assembly.GetTypes()){
				if(type.GetCustomAttribute<RpcCustomExceptionAttribute>() is not{} attribute) continue;
				
				if(!typeof(RpcException).IsAssignableFrom(type)) throw new Exception("Type "+type+" does not inherit from "+nameof(RpcException));

				//Prefer the constructor with 5 parameters, if that's not available, then check for a 4 parameter constructor
				var constructor=type.GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,null,[
					typeof(string),
					typeof(string),
					typeof(string),
					typeof(string),
					typeof(Exception),
				],null)??type.GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,null,[
					typeof(string),
					typeof(string),
					typeof(string),
					typeof(string),
				],null)??throw new Exception("Type "+type+" does not implement a "+
				                             "constructor(string? type,string? from,string? message,string? stackTrace,Exception? cause)"+
				                             ", nor a "+
				                             "constructor(string? type,string? from,string? message,string? stackTrace)");

				Constructors.Add(attribute.TypeTag,constructor);
			}
		} catch(Exception e){
			Rpc.Logger.Critical("Error registering assembly \""+assembly+"\": "+e);
		}
	}
}