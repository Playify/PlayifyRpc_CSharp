using JetBrains.Annotations;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public static partial class DynamicCaster{

	#region Fields
	public static readonly object ContinueWithNext=new();

	public delegate object? CasterDelegate(object? obj,Type type,bool throwOnError);

	/// Used to convert values beforehand
	public static readonly List<CasterDelegate> PreCasters=[
		//DefaultPreCasters.Json,
	];
	/// Used to cast values to another type. if no success, return <value>ContinueWithNext</value>
	public static readonly List<CasterDelegate> Casters=[
		//DefaultCasters.Null,
		//DefaultCasters.Primitives,
		//DefaultCasters.Char,
		//DefaultCasters.Enums,
		DefaultCasters.InstanceOfCheck,
		DefaultCasters.ImplicitConversion,
		//DefaultCasters.Json,
		//DefaultCasters.Arrays,
		//DefaultCasters.Objects,
		DefaultCasters.TryParse,
	];
	#endregion


	public static T Cast<T>(object? value)=>(T)Cast(value,typeof(T))!;

	public static object? Cast(object? value,Type type){
		foreach(var caster in PreCasters) value=caster(value,type,true);
		foreach(var caster in Casters)
			if(caster(value,type,true).Push(out var result)!=ContinueWithNext)
				return result;
		throw new RpcDataException("Error casting \""+value+"\" to "+RpcDataTypeStringifier.FromType(type),null);
	}

	public static bool TryCast<T>(object? value,out T result)=>TryCast(value,out result,false);
	public static bool TryCast(object? value,Type type,out object? result)=>TryCast(value,type,out result,false);

	public static bool TryCast<T>(object? value,out T result,bool throwOnError){
		var b=TryCast(value,typeof(T),out var o,throwOnError);
		result=b?(T)o!:default!;
		return b;
	}

	public static bool TryCast(object? value,Type type,out object? result,bool throwOnError){
		foreach(var caster in PreCasters) value=caster(value,type,true);
		foreach(var caster in Casters)
			if(caster(value,type,true).Push(out result)!=ContinueWithNext)
				return true;
		result=default;
		return false;
	}


	public static T Clone<T>(this T t) where T : ObjectTemplateBase{
		var clone=Activator.CreateInstance<T>();
		foreach(var (key,value) in t.GetProperties())
			if(!clone.TrySetProperty(key,value,true))
				throw new InvalidCastException("Error cloning "+typeof(T).Name);
		return clone;
	}
}