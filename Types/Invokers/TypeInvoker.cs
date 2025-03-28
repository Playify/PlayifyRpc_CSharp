using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public class TypeInvoker:Invoker{
	private readonly object? _instance;
	private readonly Dictionary<string,List<MethodInfo>> _methods;

	// ReSharper disable once RedundantCast
	protected TypeInvoker():this((Type)null!){
	}

	public TypeInvoker(object instance):this(instance.GetType(),instance){
	}

	public TypeInvoker(Type type,object? instance=null){
		if(type==null!){
			type=GetType();
			instance=this;
		}
		_instance=instance;
		type.RunClassConstructor();

		var bindingFlags=BindingFlags.InvokeMethod|
		                 BindingFlags.IgnoreCase|
		                 BindingFlags.Public|
		                 BindingFlags.OptionalParamBinding|
		                 BindingFlags.Static|
		                 (_instance!=null
			                  ?BindingFlags.FlattenHierarchy|
			                   BindingFlags.Instance
			                  :0);
		_methods=type.GetMethods(bindingFlags)
		             .Where(m=>m.DeclaringType!=typeof(object))
		             .Where(m=>!m.IsDefined(typeof(RpcHiddenAttribute),true))
		             .ToLookup(m=>m.GetCustomAttribute<RpcNamedAttribute>()?.Name??m.Name,StringComparer.OrdinalIgnoreCase)
		             .ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
	}

	protected sealed override object? DynamicInvoke(string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx)
		=>!_methods.TryGetValue(method,out var list)
			  ?throw new RpcMethodNotFoundException(type,method)
			  :RpcInvoker.InvokeMethod(_instance,list,type,method,args,ctx);

	protected sealed override ValueTask<string[]> GetMethods()
		=>new(_methods.Select(g=>g.Key).ToArray());

	protected sealed override ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,ProgrammingLanguage lang)
		=>_methods.TryGetValue(method,out var list)
			  ?new ValueTask<(string[] parameters,string returns)[]>(
				  list.SelectMany(m=>RpcTypeStringifier.MethodSignatures(m,lang)).ToArray())
			  :new ValueTask<(string[] parameters,string returns)[]>(
				  Task.FromException<(string[] parameters,string returns)[]>(
					  new RpcMethodNotFoundException(type,method)));
}