using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data;

/**
 * Can be used with an enum, it will be transmitted as the string Representation of the enum, useful for typescript types like "on"|"off"
 */
[PublicAPI]
public readonly struct StringEnum<T> where T:struct,Enum{
	static StringEnum(){
		DynamicData.AddConverter(x=>x is StringEnum<T> se?se.Value.ToString():x);

		RpcDataPrimitive.Register<StringEnum<T>>(
			(s,_)=>RpcDataPrimitive.From(s.Value.ToString()),
			p=>{
				if(p.IsString(out var s)&&Enum.TryParse(s,true,out T t)) return new StringEnum<T>(t);
				if(p.TryTo(out t)) return new StringEnum<T>(t);
				return RpcDataPrimitive.ContinueWithNext;
			},
			(typescript,generics)=>typescript?$"(keyof typeof {generics[0]})":$"StringEnum<{generics.Single()}>");
	}

	public readonly T Value;

	public StringEnum(T value)=>Value=value;

	public static implicit operator StringEnum<T>(T value)=>new(value);
	public static implicit operator T(StringEnum<T> value)=>value.Value;

	public override bool Equals(object? obj)=>obj is StringEnum<T> se&&se.Value.Equals(Value);

	public override int GetHashCode(){
		return Value.GetHashCode();
	}

	public override string ToString()=>Value.ToString();

	public bool Equals(StringEnum<T> other)=>Value.Equals(other.Value);

	public static bool operator==(StringEnum<T> left,StringEnum<T> right)=>left.Equals(right);

	public static bool operator!=(StringEnum<T> left,StringEnum<T> right)=>!left.Equals(right);
}

[RpcSetup]
public static class StringEnum{
	static StringEnum(){
		RpcDataPrimitive.RegisterGeneric(From,To,Stringify);
	}

	public static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already){
	}

	public static object? To(RpcDataPrimitive primitive,Type type){
	}

	public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
	}
}