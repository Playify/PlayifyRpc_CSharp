using System.Reflection;
using PlayifyRpc.Types.Exceptions;

namespace PlayifyRpc.Types;

public partial class RpcException{
	private static readonly Dictionary<string,ConstructorInfo> Constructors=new();

	static RpcException(){
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);
	}

	private static void RegisterAssembly(Assembly assembly){
		foreach(var type in assembly.GetTypes()){
			var attribute=type.GetCustomAttribute<RpcCustomExceptionAttribute>();
			if(attribute==null) continue;
			if(!typeof(RpcException).IsAssignableFrom(type)) throw new Exception("Type "+type+" does not inherit from "+nameof(RpcException));

			//Prefer the constructor with 5 parameters, if that's not available, then check for a 4 parameter constructor
			var constructor=type.GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(Exception),
			},null)??type.GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{
				typeof(string),
				typeof(string),
				typeof(string),
				typeof(string),
			},null)??throw new Exception("Type "+type+" does not implement a "+
			                             "constructor(string? type,string? from,string? message,string? stackTrace,Exception? cause)"+
			                             ", nor a "+
			                             "constructor(string? type,string? from,string? message,string? stackTrace)");

			Constructors.Add(attribute.TypeTag,constructor);
		}
	}
}