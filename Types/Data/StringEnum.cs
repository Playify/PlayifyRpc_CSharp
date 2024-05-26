using JetBrains.Annotations;

namespace PlayifyRpc.Types.Data;

/**
 * Can be used with an enum, it will be transmitted as the string Representation of the enum, useful for typescript types like "on"|"off"
 */
[PublicAPI]
public readonly struct StringEnum<T> where T:struct,Enum{
	static StringEnum()=>DynamicData.AddConverter(x=>x is StringEnum<T> se?se.Value.ToString():x);

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