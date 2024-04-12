using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;

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

	protected override object? DynamicInvoke(string? type,string method,object?[] args){
		try{
			return _type.InvokeMember(method,
			                          BindingFlags.InvokeMethod|
			                          BindingFlags.IgnoreCase|
			                          BindingFlags.Public|
			                          BindingFlags.NonPublic|
			                          BindingFlags.OptionalParamBinding|
			                          BindingFlags.Static|
			                          (_instance!=null
				                           ?BindingFlags.FlattenHierarchy|
				                            BindingFlags.Instance
				                           :0),
			                          DynamicBinder.Instance,
			                          _instance,
			                          args);
		} catch(TargetInvocationException e){
			throw RpcException.Convert(e.InnerException??e,true);
		} catch(MissingMethodException){
			throw new RpcMethodNotFoundException(type,method);
		}
	}

	protected override ValueTask<string[]> GetMethods(){
		var members=_type.GetMethods(BindingFlags.InvokeMethod|
		                             BindingFlags.IgnoreCase|
		                             BindingFlags.Public|
		                             BindingFlags.OptionalParamBinding|
		                             BindingFlags.Static|
		                             (_instance!=null?BindingFlags.Instance:0));


		return new ValueTask<string[]>(members.Where(m=>m.DeclaringType!=typeof(object)).Select(m=>m.Name).ToArray());
	}
}