using JetBrains.Annotations;

namespace PlayifyRpc.Internal.Invokers;

public class TypeInvoker:Invoker{
	private readonly Type _type;
	private readonly object? _instance;

	public TypeInvoker(Type type,object? instance=null){
		_type=type;
		_instance=instance;
	}

	[PublicAPI]
	public static TypeInvoker Create<T>(T? instance)=>new(typeof(T),instance);

	protected internal override object? DynamicInvoke(string? type,string method,object?[] args)=>StaticallyTypedUtils.InvokeMember(_type,_instance,type,method,args);
}