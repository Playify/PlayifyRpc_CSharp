using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data;

/**
 * Can be used with an enum, it will be transmitted as the string Representation of the enum, useful for typescript types like "on"|"off"
 */
[PublicAPI]
public readonly struct StringEnum<T>(T value) where T : struct,Enum{
	public readonly T Value=value;
	public static implicit operator StringEnum<T>(T value)=>new(value);
	public static implicit operator T(StringEnum<T> value)=>value.Value;
	public override bool Equals(object? obj)=>obj is StringEnum<T> se&&se.Value.Equals(Value);
	public override int GetHashCode()=>Value.GetHashCode();
	public override string ToString()=>Value.ToString();
	public bool Equals(StringEnum<T> other)=>Value.Equals(other.Value);
	public static bool operator ==(StringEnum<T> left,StringEnum<T> right)=>left.Equals(right);
	public static bool operator !=(StringEnum<T> left,StringEnum<T> right)=>!left.Equals(right);
}

[PublicAPI]
public sealed class StringEnumAttribute:RpcDataTransformerAttribute{
	public override RpcDataPrimitive? From(object? o,[RpcDataNullTransformer]RpcDataPrimitive.Already? already){
		if(o==null||!o.GetType().IsEnum) return null;
		return new RpcDataPrimitive(o.ToString()!);
	}

	public override bool? TryTo(RpcDataPrimitive value,Type type,out object? result,bool throwOnError){
		if(value.IsString(out var s)&&type.IsEnum)
			return StringEnums.TryParseEnum(type,s,out result);
		result=null;
		return null;
	}

	public override string? StringifyType(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
		if(!type.IsEnum) return null;
		return RpcTypeStringifier.CombineTypeName(typeof(StringEnum<>),[type.Name]);
	}

	public override bool Equals(RpcDataTransformerAttribute other)=>other is StringEnumAttribute;
	public override int GetHashCode()=>typeof(StringEnumAttribute).GetHashCode();
}

[RpcSetup]
[PublicAPI]
public static class StringEnums{
	static StringEnums(){
		RpcData.Register(
			typeof(StringEnum<>),
			(o,_,_)=>new RpcDataPrimitive($"{o}"),
			(primitive,type,_,_)=>{
				if(primitive.IsString(out var s)&&
				   TryParseEnum(type.GetGenericArguments()[0],s,out var result))
					return Activator.CreateInstance(type,result);
				return RpcData.ContinueWithNext;
			},
			(_,generics)=>RpcTypeStringifier.CombineTypeName(typeof(StringEnum<>),generics)
		);
	}

	public static StringEnum<T> Wrap<T>(T t) where T : struct,Enum=>new(t);

	internal static bool TryParseEnum(Type enumType,string s,out object? result){
#if NETFRAMEWORK
		try{
			result=Enum.Parse(enumType,s,true);
			return true;
		} catch(ArgumentException){
			result=null;
			return false;
		} catch(OverflowException){
			result=null;
			return false;
		}
#else
		return Enum.TryParse(enumType,s,true,out result);
#endif
	}
}