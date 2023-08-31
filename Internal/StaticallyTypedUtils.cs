using System.Dynamic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PlayifyRpc.Types.Data;
using PlayifyUtils.Jsons;
using PlayifyUtils.Utils;

namespace PlayifyRpc.Internal;

public static class StaticallyTypedUtils{
	public static async Task<object?> UnwrapTask(object? result){
		while(result is Task task){
			await task;
			var property=result.GetType().GetProperty("Result");
			result=property?.GetValue(result);
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


	internal static IList<Type>? GetGenericTypeArguments(InvokeMemberBinder binder){
		if(Type.GetType("Mono.Runtime")!=null){
			// In mono this is trivial.

			// First we get field info.
			var field=binder.GetType().GetField("typeArguments",BindingFlags.Instance|
			                                                    BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);

			// If this was a success get and return it's value
			return field?.GetValue(binder) as IList<Type>;
		} else{
			// In this case, we need more aerobic :D

			// First, get the interface
			var inter=binder.GetType().GetInterface("Microsoft.CSharp.RuntimeBinder.ICSharpInvokeOrInvokeMemberBinder");

			// Now get property.
			var prop=inter?.GetProperty("TypeArguments");

			// If we have a property, return it's value
			return prop?.GetValue(binder,null) as IList<Type>;
		}
	}

	private static readonly Type[] ValueTupleTypes={
		typeof(ValueTuple),
		typeof(ValueTuple<>),
		typeof(ValueTuple<,>),
		typeof(ValueTuple<,,>),
		typeof(ValueTuple<,,,>),
		typeof(ValueTuple<,,,,>),
		typeof(ValueTuple<,,,,,>),
		typeof(ValueTuple<,,,,,,>),
		typeof(ValueTuple<,,,,,,,>),
	};

	public static bool CanCast(Type? from,Type to,object? fromInstance){
		if(to.IsPrimitive) return from!=null&&DynamicBinder.CanChangePrimitive(fromInstance?.GetType()??from,to,fromInstance);

		if(from==null) return true;

		if(to.IsAssignableFrom(from)) return true;

		if(from.IsCOMObject&&to.IsInstanceOfType(fromInstance)) return true;

		if(from.IsArray&&fromInstance is Array array){
			if(to.IsArray){
				var elementType=to.GetElementType()!;
				return array.Cast<object?>().All(o=>CanCast(o?.GetType(),elementType,o));
			}
			if(to.IsGenericType&&ValueTupleTypes.Contains(to.GetGenericTypeDefinition())){
				var types=to.GetGenericArguments();
				if(array.Length!=types.Length) return false;//Length mismatch
				return Enumerable.Zip(array.Cast<object?>(),types).All(tuple=>CanCast(tuple.First?.GetType(),tuple.Second,tuple.First));
			}
			
			
			if(to.IsAssignableFrom(typeof(JsonArray))){
				return array.Cast<object?>().All(o=>CanCast(o?.GetType(),typeof(Json),o));
			}
		}

		if(fromInstance is ExpandoObject expando){
			if(to.IsAssignableTo(typeof(DataTemplate))) return true;
			
			if(to.IsAssignableFrom(typeof(JsonObject))){
				return ((IDictionary<string,object?>)expando).Values.All(o=>CanCast(o?.GetType(),typeof(Json),o));
			}
		}


		return false;

	}

	public static T DoCast<T>(object? value)=>(T)DoCast(value,typeof(T));

	public static object DoCast(object? value,Type type){
		if(value==null) return value!;
		if(type.IsPrimitive) return Convert.ChangeType(value,Type.GetTypeCode(type));
		if(type.IsEnum) return Enum.ToObject(type,Convert.ChangeType(value,Type.GetTypeCode(type)));
		if(type.IsInstanceOfType(value)) return value;

		if(value is Array array){
			if(type.IsArray){
				var elementType=type.GetElementType()!;
				var target=Array.CreateInstance(elementType,array.Length);
				var i=0;
				foreach(var o in array){
					target.SetValue(DoCast(o,elementType),i);
					i++;
				}
				return target;
			}
			if(type.IsAssignableFrom(typeof(JsonArray))){
				return new JsonArray(array.Cast<object?>().Select(DoCast<Json>));
			}
		}
		if(type.IsGenericType&&ValueTupleTypes.Contains(type.GetGenericTypeDefinition())&&value is Array tupleArray){
			var constructor=type.GetConstructor(type.GetGenericArguments());
			var parameters=tupleArray.Cast<object?>().Zip(type.GetGenericArguments(),DoCast).ToArray();
			return constructor!.Invoke(parameters);
		}

		if(type.IsAssignableTo(typeof(DataTemplate))&&value is ExpandoObject expando) return DataTemplate.CreateFromExpando(expando,type);

		throw new Exception("Error casting \""+value+"\" to "+type.Name);
	}
}