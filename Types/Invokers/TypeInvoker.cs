using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public class TypeInvoker:Invoker{
	private readonly object? _instance;
	private readonly Type _type;

	private BindingFlags BindingFlags
		=>BindingFlags.InvokeMethod|
		  BindingFlags.IgnoreCase|
		  BindingFlags.Public|
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

	protected sealed override object? DynamicInvoke(string? type,string method,RpcDataPrimitive[] args){
		try{
			return _type.InvokeMember(method,
				BindingFlags,
				DynamicBinder.Instance,
				_instance,
				args);
		} catch(TargetInvocationException e){
			throw RpcException.WrapAndFreeze(e.InnerException??e);
		} catch(MissingMethodException){
			if(GetMethodsDirect().Any(m=>m.Equals(method,StringComparison.OrdinalIgnoreCase)))
				throw new RpcMethodNotFoundException(type,method,"Method doesn't accept "+args.Length+" arguments");
			throw new RpcMethodNotFoundException(type,method);
		} catch(MethodAccessException e){
			throw new RpcMethodNotFoundException(type,method,e.Message);
		} catch(AmbiguousMatchException){
			throw new RpcMethodNotFoundException(type,method,"Call is ambiguous"){Data={{"ambiguous",true}}};
		} catch(RpcDataException e){
			throw new RpcMethodNotFoundException(type,method,"Error casting arguments",e);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e);
		}
	}

	private IEnumerable<string> GetMethodsDirect()=>_type.GetMethods(BindingFlags)
	                                                     .Where(m=>m.DeclaringType!=typeof(object))//in DynamicInvoke, this is handled inside the DynamicBinder
	                                                     .Where(m=>m.GetCustomAttribute<RpcHiddenAttribute>()==null)//in DynamicInvoke, this is handled inside the DynamicBinder
	                                                     .Select(m=>m.Name)
	                                                     .Distinct();

	protected sealed override ValueTask<string[]> GetMethods()=>new(GetMethodsDirect().ToArray());

	protected sealed override ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,bool ts){
		var signatures=_type.GetMethods(BindingFlags)
		                    .Where(m=>m.DeclaringType!=typeof(object))//in DynamicInvoke, this is handled inside the DynamicBinder
		                    .Where(m=>m.GetCustomAttribute<RpcHiddenAttribute>()==null)//in DynamicInvoke, this is handled inside the DynamicBinder
		                    .Where(m=>m.Name.Equals(method,StringComparison.OrdinalIgnoreCase))
		                    .SelectMany(m=>RpcDataTypeStringifier.MethodSignatures(m,ts))
		                    .ToArray();
		return signatures.Length==0
			       ?new ValueTask<(string[] parameters,string returns)[]>(Task.FromException<(string[] parameters,string returns)[]>(new RpcMethodNotFoundException(type,method)))
			       :new ValueTask<(string[] parameters,string returns)[]>(signatures);
	}
}

[PublicAPI]
public class TypeInvoker<T>:TypeInvoker{
	public TypeInvoker():base(typeof(T)){}
	public TypeInvoker(T? instance):base(typeof(T),instance){}
}