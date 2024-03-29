﻿using System.Dynamic;
using System.Reflection;
using PlayifyRpc.Types.Data;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

public static partial class StaticallyTypedUtils{
	public static async Task<object?> UnwrapTask(object? result){
		while(result is Task task){
			await task;
			result=result.GetType().GetProperty("Result")?.GetValue(result);
			if(result is VoidType) result=null;
			if(result?.GetType().FullName=="System.Threading.Tasks.VoidTaskResult") result=null;
		}
		return result;
	}

	internal static object? InvokeMember(Type instanceType,object? instance,string? type,string method,object?[] args){
		try{
			return instanceType.InvokeMember(method,
			                                 BindingFlags.InvokeMethod|
			                                 BindingFlags.IgnoreCase|
			                                 BindingFlags.Public|
			                                 BindingFlags.NonPublic|
			                                 BindingFlags.OptionalParamBinding|
			                                 BindingFlags.Static|
			                                 (instance!=null
				                                  ?BindingFlags.FlattenHierarchy|
				                                   BindingFlags.Instance
				                                  :0),
			                                 DynamicBinder.Instance,
			                                 instance,
			                                 args);
		} catch(TargetInvocationException e){
			throw (e.InnerException??e).Rethrow();
		} catch(MissingMethodException){
			throw new MissingMethodException($"Unknown Method on type {type??"null"} : {instanceType.FullName}.{method}({args.Select(a=>a?.GetType().Name??"null").Join(",")})");
		}
	}


	internal static IList<Type>? GetGenericTypeArguments(InvokeMemberBinder binder)
		=>Type.GetType("Mono.Runtime")!=null
			  ?binder
			   .GetType()
			   .GetField("typeArguments",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
			   ?.GetValue(binder) as IList<Type>
			  :binder
			   .GetType()
			   .GetInterface("Microsoft.CSharp.RuntimeBinder.ICSharpInvokeOrInvokeMemberBinder")
			   ?.GetProperty("TypeArguments")
			   ?.GetValue(binder,null) as IList<Type>;

	public static ValueTask<string[]> GetMembers(Type type,object? instance){
		var members=type.GetMethods(BindingFlags.InvokeMethod|
		                            BindingFlags.IgnoreCase|
		                            BindingFlags.Public|
		                            BindingFlags.OptionalParamBinding|
		                            BindingFlags.Static|
		                            (instance!=null?BindingFlags.Instance:0));


		return new ValueTask<string[]>(members.Where(m=>m.DeclaringType!=typeof(object)).Select(m=>m.Name).ToArray());
	}
}