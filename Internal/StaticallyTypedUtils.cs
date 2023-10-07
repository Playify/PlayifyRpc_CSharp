using System.Dynamic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PlayifyRpc.Types.Data;
using PlayifyUtils.Utils;

namespace PlayifyRpc.Internal;

public static partial class StaticallyTypedUtils{
	public static async Task<object?> UnwrapTask(object? result){
		while(result is Task task){
			await task;
			result=result.GetType().GetProperty("Result")?.GetValue(result);
			if(result?.GetType().FullName=="System.Threading.Tasks.VoidTaskResult") result=null;
		}
		return result;
	}

	internal static object? InvokeMember(Type instanceType,object? instance,string? type,string method,object?[] args){
		try{
			return instanceType.InvokeMember(method,BindingFlags.InvokeMethod|
			                                        BindingFlags.IgnoreCase|
			                                        BindingFlags.Public|
			                                        BindingFlags.NonPublic|
			                                        BindingFlags.OptionalParamBinding|
			                                        BindingFlags.FlattenHierarchy|
			                                        BindingFlags.Static|
			                                        (instance!=null?BindingFlags.Instance:0),
			                                 DynamicBinder.Instance,instance,args);
		} catch(TargetInvocationException e){
			ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();
			throw e.InnerException;
		} catch(MissingMethodException){
			throw new MissingMethodException($"Unknown Method on type {(type??"null")} : {instanceType.FullName}.{method}({args.Select(a=>a?.GetType().Name??"null").Join(",")})");
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
}