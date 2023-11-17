using System.Dynamic;
using System.Reflection;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;

namespace PlayifyRpc.Internal;

public static partial class StaticallyTypedUtils{
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

	public static bool TryCast<T>(object? value,out T result){
		if(TryCast(value,typeof(T),out var res)){
			result=(T) res!;
			return true;
		}
		result=default!;
		return false;
	}

	public static bool TryCast(object? value,Type type,out object? result){
		if(value is null or JsonNull){
			result=type.IsAssignableFrom(typeof(JsonNull))?JsonNull.Null:null;
			return true;
		}
		return DoCast(value,type).NotNull<object>(out result);
	}

	public static T Cast<T>(object? value)=>(T) Cast(value,typeof(T));

	public static object Cast(object? value,Type type)
		=>TryCast(value,type,out var result)
		  ?result!
		  :throw new InvalidCastException("Error casting \""+value+"\" to "+type.Name);

	private static object? DoCast(object value,Type type){
		value=value switch{
			//Json
			JsonString j=>j.Value,
			JsonNumber j=>j.Value,
			JsonBool j=>j.Value,
			//JsonNull is handled in TryCast
			_=>value,
		};


		//Primitives
		if(type.IsPrimitive)
			try{
				return Convert.ChangeType(value,Type.GetTypeCode(type));
			} catch(Exception){
				return null;
			}
		//Enums
		if(type.IsEnum)
			try{
				if(value is string valueAsString&&Enum.TryParse(type,valueAsString,true,out var result))
					return result;
				return Enum.ToObject(type,Convert.ChangeType(value,Type.GetTypeCode(type)));
			} catch(Exception){
				return null;
			}
		//Instanceof check
		if(type.IsInstanceOfType(value)) return value;

		//Json
		if(type.IsAssignableFrom(typeof(JsonString))&&value is string s) return new JsonString(s);
		if(type.IsAssignableFrom(typeof(JsonBool))&&value is bool b) return b?JsonBool.True:JsonBool.False;
		if(type.IsAssignableFrom(typeof(JsonNumber))&&TryCast<double>(value,out var d)) return new JsonNumber(d);


		//Arrays & Tuples
		if(value is Array array){
			if(type.IsArray){
				var elementType=type.GetElementType()!;
				var target=Array.CreateInstance(elementType,array.Length);
				var i=0;
				foreach(var o in array){
					if(!TryCast(o,elementType,out var element)) return null;
					target.SetValue(element,i);
					i++;
				}
				return target;
			}
			if(type.IsGenericType&&ValueTupleTypes.Contains(type.GetGenericTypeDefinition())){
				var argsTypes=type.GetGenericArguments();
				if(array.Length!=argsTypes.Length) return null;

				var args=new object?[array.Length];
				for(var i=0;i<array.Length;i++)
					if(TryCast(array.GetValue(i),argsTypes[i],out var element))
						args[i]=element;
					else return null;


				return type.GetConstructor(argsTypes)!.Invoke(args);
			}
			if(type.IsAssignableFrom(typeof(JsonArray))){
				var jsonArray=new JsonArray();
				foreach(var o in array){
					if(TryCast<Json>(o,out var element))
						jsonArray.Add(element);
					else return null;
				}
				return jsonArray;
			}
			return null;
		}
		//Object
		if(value is ExpandoObject exp){
			if(type.IsAssignableTo(typeof(ObjectTemplate))) return ObjectTemplate.CreateFromExpando(exp,type);


			if(type.IsAssignableFrom(typeof(JsonObject))){
				var o=new JsonObject();
				foreach(var (key,val) in exp){
					if(TryCast<Json>(val,out var element))
						o[key]=element;
					else return null;
				}
				return o;
			}
			return null;
		}
		
		if(value is string&&type.GetMethod("TryParse",
		                                               BindingFlags.Public|BindingFlags.Static|BindingFlags.InvokeMethod,
		                                               new[]{typeof(string),type.MakeByRefType()}) is {} tryParse){
			var parameters=new[]{value,type.IsValueType?Activator.CreateInstance(type):null};
			if(tryParse.Invoke(null,parameters) is true)
				return parameters[1];
		}

		return null;
	}
}