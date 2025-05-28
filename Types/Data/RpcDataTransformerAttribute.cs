using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data;

/**
 * Used to change the way values are serialized
 */
[PublicAPI]
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Parameter|AttributeTargets.ReturnValue)]
public abstract class RpcDataTransformerAttribute:Attribute{

	public abstract RpcDataPrimitive? From(object? o,RpcDataPrimitive.Already? already);
	public abstract bool? TryTo(RpcDataPrimitive value,Type type,out object? result,bool throwOnError);
	public abstract string? StringifyType(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics);

	public abstract bool Equals(RpcDataTransformerAttribute other);


	public override bool Equals(object? obj)=>ReferenceEquals(this,obj)||obj is RpcDataTransformerAttribute other&&Equals(other);
	public override abstract int GetHashCode();

	public static bool operator ==(RpcDataTransformerAttribute? left,RpcDataTransformerAttribute? right)=>Equals(left,right);

	public static bool operator !=(RpcDataTransformerAttribute? left,RpcDataTransformerAttribute? right)=>!Equals(left,right);

	public sealed class RpcDataNullTransformer:RpcDataTransformerAttribute{
		public static readonly RpcDataNullTransformer Instance=new();

		public override RpcDataPrimitive? From(object? o,RpcDataPrimitive.Already? already)=>new RpcDataPrimitive();

		public override bool? TryTo(RpcDataPrimitive value,Type type,out object? result,bool throwOnError){
			result=type.IsValueType?Activator.CreateInstance(type):null;
			return true;
		}

		public override string? StringifyType(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics)
			=>input?"null":"void";

		public override bool Equals(RpcDataTransformerAttribute other)=>other is RpcDataNullTransformer;

		public override int GetHashCode()=>typeof(RpcDataNullTransformer).GetHashCode();
	}
}