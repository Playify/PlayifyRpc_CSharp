using System.Dynamic;
using System.Reflection;
using PlayifyRpc.Types.Data.Objects;
using PlayifyUtility.Jsons;

namespace PlayifyRpc.Internal.Data;

public static partial class DynamicCaster{
	private static IEnumerable<(string,object?)>? TryGetObjectProps(object? x)
		=>x switch{
			// ReSharper disable once RedundantCast
			ExpandoObject exp=>exp.Select(pair=>(pair.Key,(object?)pair.Value)),
			JsonObject jsonObject=>jsonObject.Select(pair=>(pair.Key,(object?)pair.Value)),
			ObjectTemplateBase obj=>obj.GetProperties(),
			_=>null,
		};

	public static readonly List<(Type,object?)> Nulls=[
		(typeof(JsonNull),JsonNull.Null),
	];
	private static readonly Type[] ValueTupleTypes=[
		typeof(ValueTuple),
		typeof(ValueTuple<>),
		typeof(ValueTuple<,>),
		typeof(ValueTuple<,,>),
		typeof(ValueTuple<,,,>),
		typeof(ValueTuple<,,,,>),
		typeof(ValueTuple<,,,,,>),
		typeof(ValueTuple<,,,,,,>),
		typeof(ValueTuple<,,,,,,,>),
	];


	private static class DefaultPreCasters{
		public static object? Json(object? value,Type type,bool throwOnError)
			=>value switch{
				JsonString j=>j.Value,
				JsonNumber j=>j.Value,
				JsonBool j=>j.Value,
				JsonNull=>null,
				_=>value,
			};
	}

	private static class DefaultCasters{
		public static object? Null(object? value,Type type,bool throwOnError){
			if(value!=null) return ContinueWithNext;
			if(type==typeof(object)) return null;
			foreach(var (t,v) in Nulls)
				if(type.IsAssignableFrom(t))
					return v;
			//Reference types
			if(!type.IsValueType) return null;
			//Nullable value types
			if(Nullable.GetUnderlyingType(type)!=null) return null;
			//Value types, that don't support null
			return ContinueWithNext;
		}

		public static object Primitives(object? value,Type type,bool throwOnError){
			if(!type.IsPrimitive||
			   type==typeof(bool)||
			   type==typeof(DateTime)||
			   !TryCast(value,out IConvertible number,throwOnError)) return ContinueWithNext;
			try{
				return Convert.ChangeType(number,Type.GetTypeCode(type));
			} catch(Exception){
				return ContinueWithNext;
			}
		}

		public static object Enums(object? value,Type type,bool throwOnError){
			if(!type.IsEnum) return ContinueWithNext;
			try{
				if(TryCast(value,out string valueAsString)){
#if NETFRAMEWORK
					try{
						return Enum.Parse(type,valueAsString,true);
					} catch{
						// ignored
					}
#else
					if(Enum.TryParse(type,valueAsString,true,out var result))
						return result!;
#endif
				}

				if(!TryCast(value,out IConvertible number)) return ContinueWithNext;

				return Enum.ToObject(type,Convert.ChangeType(number,Type.GetTypeCode(type)));
			} catch(Exception){
				return ContinueWithNext;
			}
		}

#if NETFRAMEWORK
		public static object? InstanceOfCheck(object? value,Type type,bool throwOnError)=>type.IsInstanceOfType(value)?value:ContinueWithNext;
#else
		public static object InstanceOfCheck(object? value,Type type,bool throwOnError)=>type.IsInstanceOfType(value)?value:ContinueWithNext;
#endif


		public static object? ImplicitConversion(object? value,Type type,bool throwOnError){
			IEnumerable<MethodInfo> methods=type.GetMethods(BindingFlags.Public|BindingFlags.Static);
			if(value!=null) methods=value.GetType().GetMethods(BindingFlags.Public|BindingFlags.Static).Concat(methods);
			foreach(var mi in methods){
				if(mi.Name!="op_Implicit") continue;
				if(!type.IsAssignableFrom(mi.ReturnType)) continue;
				if(mi.GetParameters().FirstOrDefault() is not{} param) continue;
				if(TryCast(value,param.ParameterType,out var par,false)){
					return mi.Invoke(null,[par]);
				}
			}
			return ContinueWithNext;
		}

		public static object Json(object? value,Type type,bool throwOnError){
			if(type.IsAssignableFrom(typeof(JsonString))&&value is string s) return new JsonString(s);
			if(type.IsAssignableFrom(typeof(JsonBool))&&value is bool b) return b?JsonBool.True:JsonBool.False;
			if(type.IsAssignableFrom(typeof(JsonNumber))&&TryCast(value,out double d,false)) return new JsonNumber(d);
			if(type.IsAssignableFrom(typeof(JsonNull))&&value is null or JsonNull) return JsonNull.Null;
			return ContinueWithNext;
		}

		public static object Arrays(object? value,Type type,bool throwOnError){
			if(value is not Array array) return ContinueWithNext;

			if(type.IsArray){
				var elementType=type.GetElementType()!;
				var target=Array.CreateInstance(elementType,array.Length);
				var i=0;
				foreach(var o in array){
					if(!TryCast(o,elementType,out var element,throwOnError)) return ContinueWithNext;
					target.SetValue(element,i);
					i++;
				}
				return target;
			}
			if(type.IsGenericType&&ValueTupleTypes.Contains(type.GetGenericTypeDefinition())){
				var argsTypes=type.GetGenericArguments();
				if(array.Length!=argsTypes.Length) return ContinueWithNext;

				var args=new object?[array.Length];
				for(var i=0;i<array.Length;i++)
					if(TryCast(array.GetValue(i),argsTypes[i],out var element,throwOnError))
						args[i]=element;
					else return ContinueWithNext;


				return type.GetConstructor(argsTypes)!.Invoke(args);
			}
			if(type.IsAssignableFrom(typeof(JsonArray))){
				var jsonArray=new JsonArray();
				foreach(var o in array)
					if(TryCast(o,out Json element,throwOnError))
						jsonArray.Add(element);
					else return ContinueWithNext;
				return jsonArray;
			}
			return ContinueWithNext;
		}

		public static object Objects(object? value,Type type,bool throwOnError){
			if(typeof(ObjectTemplateBase).IsAssignableFrom(type)&&TryGetObjectProps(value) is{} props){
				var o=(ObjectTemplateBase)Activator.CreateInstance(type)!;
				foreach(var (k,v) in props)
					if(!o.TrySetProperty(k,v,throwOnError))
						return ContinueWithNext;
				return o;
			}
			if(type.IsAssignableFrom(typeof(JsonObject))&&(props=TryGetObjectProps(value))!=null){
				var o=new JsonObject();
				foreach(var (k,v) in props)
					if(TryCast(v,out Json casted,throwOnError)) o[k]=casted;
					else return ContinueWithNext;
				return o;
			}
			if(typeof(ExpandoObject).IsAssignableFrom(type)&&(props=TryGetObjectProps(value))!=null){
				var o=new ExpandoObject();
				foreach(var (k,v) in props)
					((IDictionary<string,object?>)o)[k]=TryCast(v,out object? casted,false)?casted:v;
				return o;
			}
			return ContinueWithNext;
		}

		public static object? TryParse(object? value,Type type,bool throwOnError){
			if(type.GetMethod("TryParse",
			                  BindingFlags.Public|BindingFlags.Static|BindingFlags.InvokeMethod,
			                  null,
			                  [typeof(string),type.MakeByRefType()],
			                  null) is not{} tryParse) return ContinueWithNext;
			if(!TryCast(value,out string s)) return ContinueWithNext;

			var parameters=new[]{s,type.IsValueType?Activator.CreateInstance(type):null};
			if(tryParse.Invoke(null,parameters) is true)
				return parameters[1];
			return ContinueWithNext;
		}
	}
}