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

[RpcSetup]
[PublicAPI]
public static class StringEnum{
	static StringEnum(){
		RpcDataPrimitive.Register(
			typeof(StringEnum<>),
			(o,_)=>new RpcDataPrimitive(o.ToString()),
			(primitive,type)=>{
				if(!primitive.IsString(out var s)) return RpcDataPrimitive.ContinueWithNext;
				if(!TryParseEnum(type.GetGenericArguments()[0],s,out var result)) return RpcDataPrimitive.ContinueWithNext;
				return Activator.CreateInstance(type,result);
			},
			(_,generics)=>RpcDataTypeStringifier.TypeName(typeof(StringEnum),generics)
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