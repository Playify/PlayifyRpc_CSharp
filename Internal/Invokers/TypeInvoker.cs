using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class TypeInvoker:Invoker{
	private readonly object? _instance;
	private readonly Type _type;

	public TypeInvoker(Type type,object? instance=null){
		_type=type;
		_instance=instance;
		RuntimeHelpers.RunClassConstructor(type.TypeHandle);
	}

	[PublicAPI]
	public static TypeInvoker Create<T>(T? instance)=>new(typeof(T),instance);

	protected internal override object? DynamicInvoke(string? type,string method,object?[] args)=>StaticallyTypedUtils.InvokeMember(_type,_instance,type,method,args);
}