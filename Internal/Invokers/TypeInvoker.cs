using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class TypeInvoker:Invoker{
	private readonly object? _instance;
	private readonly Type _type;

	private BindingFlags BindingFlags=>BindingFlags.InvokeMethod|
	                                   BindingFlags.IgnoreCase|
	                                   BindingFlags.Public|
	                                   BindingFlags.NonPublic|
	                                   BindingFlags.OptionalParamBinding|
	                                   BindingFlags.Static|
	                                   (_instance!=null
		                                    ?BindingFlags.FlattenHierarchy|
		                                     BindingFlags.Instance
		                                    :0);

	protected TypeInvoker(){
		_type=GetType();
		_instance=this;
	}

	public TypeInvoker(object instance):this(instance.GetType(),instance){
	}

	public TypeInvoker(Type type,object? instance=null){
		_type=type;
		_instance=instance;
		type.RunClassConstructor();
	}

	[PublicAPI]
	public static TypeInvoker Create<T>(T? instance)=>new(typeof(T),instance);

	protected override object? DynamicInvoke(string? type,string method,object?[] args){
		try{
			return _type.InvokeMember(method,
				BindingFlags,
				DynamicBinder.Instance,
				_instance,
				args);
		} catch(TargetInvocationException e){
			throw RpcException.WrapAndFreeze(e.InnerException??e);
		} catch(MissingMethodException){
			throw new RpcMethodNotFoundException(type,method);
		}
	}

	protected override ValueTask<string[]> GetMethods()
		=>new(_type.GetMethods(BindingFlags)
		           .Where(m=>m.DeclaringType!=typeof(object))//in DynamicInvoke, this is handled inside the DynamicBinder
		           .Select(m=>m.Name)
		           .Distinct()
		           .ToArray());
}

[PublicAPI]
public class TypeInvoker<T>:TypeInvoker{
	public TypeInvoker():base(typeof(T)){}
	public TypeInvoker(T? instance):base(typeof(T),instance){}
}