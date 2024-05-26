using System.Dynamic;
using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Jsons;

namespace PlayifyRpc.Internal;

[PublicAPI]
public static partial class StaticallyTypedUtils{
	public static readonly object ContinueWithNext=new();
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
	private static readonly List<Func<object?,Type,object?>> PreCasters=new();
	private static readonly List<Func<object?,Type,object?>> Casters=new();


	public static T Cast<T>(object? value)=>(T)Cast(value,typeof(T));

	public static object Cast(object? value,Type type)
		=>TryCast(value,type,out var result)
			  ?result!
			  :throw new InvalidCastException("Error casting \""+value+"\" to "+type.Name);

	public static bool TryCast<T>(object? value,out T result){
		if(TryCast(value,typeof(T),out var res)){
			result=(T)res!;
			return true;
		}
		result=default!;
		return false;
	}

	public static bool TryCast(object? value,Type type,out object? result){
		foreach(var caster in PreCasters){
			var newValue=caster(value,type);
			if(newValue!=ContinueWithNext) value=newValue;
			else throw new Exception(nameof(RegisterPre)+" converters are not allowd to return "+nameof(ContinueWithNext));
		}
		foreach(var caster in Casters){
			result=caster(value,type);
			if(result!=ContinueWithNext) return true;
		}
		result=null;
		return false;
	}

	/// Used to convert values beforehand
	public static void RegisterPre(Func<object?,Type,object?> converter)=>PreCasters.Add(converter);

	/// Used to cast values to another type. if no success, return <value>ContinueWithNext</value>
	public static void Register(Func<object?,Type,object?> caster)=>Casters.Add(caster);


	static StaticallyTypedUtils(){
		RegisterPre(Pre.Json);
		Register(Methods.Null);
		Register(Methods.Primitives);
		Register(Methods.Enums);
		Register(Methods.InstanceOfCheck);
		Register(Methods.ImplicitConversion);
		Register(Methods.Json);
		Register(Methods.Arrays);
		Register(Methods.Objects);
		Register(Methods.TryParse);
	}

	private static IEnumerable<(string,object?)>? GetObjectProps(object? x)
		=>x switch{
			ExpandoObject exp=>exp.Select(pair=>(pair.Key,(object?)pair.Value)),
			JsonObject jsonObject=>jsonObject.Select(pair=>(pair.Key,(object?)pair.Value)),
			ObjectTemplate obj=>obj.GetProperties(),
			_=>null,
		};

	private static readonly List<(Type,object?)> Nulls=new(){
		(typeof(JsonNull),JsonNull.Null),
	};


	private static class Pre{
		public static object? Json(object? value,Type type)
			=>value switch{
				JsonString j=>j.Value,
				JsonNumber j=>j.Value,
				JsonBool j=>j.Value,
				JsonNull=>null,
				_=>value,
			};
	}


	private static class Methods{
		public static object? Null(object? value,Type type){
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

		public static object Primitives(object? value,Type type){
			if(!type.IsPrimitive||!TryCast(value,out IConvertible number)) return ContinueWithNext;
			try{
				return Convert.ChangeType(number,Type.GetTypeCode(type));
			} catch(Exception){
				return ContinueWithNext;
			}
		}

		public static object Enums(object? value,Type type){
			if(!type.IsEnum) return ContinueWithNext;
			try{
#if NETFRAMEWORK
				if(TryCast(value,out string valueAsString))
					try{
						return Enum.Parse(type,valueAsString,true);
					} catch{
						// ignored
					}
#else
				if(TryCast(value,out string valueAsString)&&Enum.TryParse(type,valueAsString,true,out var result))
					return result!;
#endif
				if(!TryCast(value,out IConvertible number)) return ContinueWithNext;

				return Enum.ToObject(type,Convert.ChangeType(number,Type.GetTypeCode(type)));
			} catch(Exception){
				return ContinueWithNext;
			}
		}

		public static object? InstanceOfCheck(object? value,Type type)=>type.IsInstanceOfType(value)?value:ContinueWithNext;


		public static object? ImplicitConversion(object? value,Type type){
			IEnumerable<MethodInfo> methods=type.GetMethods(BindingFlags.Public|BindingFlags.Static);
			if(value!=null) methods=value.GetType().GetMethods(BindingFlags.Public|BindingFlags.Static).Concat(methods);
			foreach(var mi in methods){
				if(mi.Name!="op_Implicit") continue;
				if(!type.IsAssignableFrom(mi.ReturnType)) continue;
				if(mi.GetParameters().FirstOrDefault() is not{} param) continue;
				if(TryCast(value,param.ParameterType,out var par)){
					return mi.Invoke(null,new[]{par});
				}
			}
			return ContinueWithNext;
		}

		public static object Json(object? value,Type type){
			if(type.IsAssignableFrom(typeof(JsonString))&&value is string s) return new JsonString(s);
			if(type.IsAssignableFrom(typeof(JsonBool))&&value is bool b) return b?JsonBool.True:JsonBool.False;
			if(type.IsAssignableFrom(typeof(JsonNumber))&&TryCast<double>(value,out var d)) return new JsonNumber(d);
			if(type.IsAssignableFrom(typeof(JsonNull))&&value is null or JsonNull) return JsonNull.Null;
			return ContinueWithNext;
		}

		public static object Arrays(object? value,Type type){
			if(value is not Array array) return ContinueWithNext;

			if(type.IsArray){
				var elementType=type.GetElementType()!;
				var target=Array.CreateInstance(elementType,array.Length);
				var i=0;
				foreach(var o in array){
					if(!TryCast(o,elementType,out var element)) return ContinueWithNext;
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
					if(TryCast(array.GetValue(i),argsTypes[i],out var element))
						args[i]=element;
					else return ContinueWithNext;


				return type.GetConstructor(argsTypes)!.Invoke(args);
			}
			if(type.IsAssignableFrom(typeof(JsonArray))){
				var jsonArray=new JsonArray();
				foreach(var o in array)
					if(TryCast<Json>(o,out var element))
						jsonArray.Add(element);
					else return ContinueWithNext;
				return jsonArray;
			}
			return ContinueWithNext;
		}

		public static object Objects(object? value,Type type){
			if(typeof(ObjectTemplate).IsAssignableFrom(type))
				return GetObjectProps(type) is{} props?ObjectTemplate.TryCreateTemplate(props,type)??ContinueWithNext:ContinueWithNext;
			if(type.IsAssignableFrom(typeof(JsonObject)))
				return GetObjectProps(type) is{} props?ObjectTemplate.TryCreateJson(props)??ContinueWithNext:ContinueWithNext;
			if(typeof(ExpandoObject).IsAssignableFrom(type))
				return GetObjectProps(type) is{} props?ObjectTemplate.TryCreateExpando(props):ContinueWithNext;
			return ContinueWithNext;
		}

		public static object? TryParse(object? value,Type type){
			if(type.GetMethod("TryParse",
			                  BindingFlags.Public|BindingFlags.Static|BindingFlags.InvokeMethod,
			                  null,
			                  new[]{typeof(string),type.MakeByRefType()},
			                  null) is not{} tryParse) return ContinueWithNext;
			if(!TryCast(value,out string s)) return ContinueWithNext;

			var parameters=new[]{s,type.IsValueType?Activator.CreateInstance(type):null};
			if(tryParse.Invoke(null,parameters) is true)
				return parameters[1];
			return ContinueWithNext;
		}
	}
}