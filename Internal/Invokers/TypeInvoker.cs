using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class TypeInvoker:Invoker{
	private readonly object? _instance;
	private readonly Type _type;

	protected TypeInvoker(){
		_type=GetType();
		_instance=this;
	}

	public TypeInvoker(Type type,object? instance=null){
		_type=type;
		_instance=instance;
		RuntimeHelpers.RunClassConstructor(type.TypeHandle);
	}

	[PublicAPI]
	public static TypeInvoker Create<T>(T? instance)=>new(typeof(T),instance);

	protected override object? DynamicInvoke(string? type,string method,object?[] args)=>StaticallyTypedUtils.InvokeMember(_type,_instance,type,method,args);

	protected override ValueTask<string[]> GetMethods()=>StaticallyTypedUtils.GetMembers(_type,_instance);
}