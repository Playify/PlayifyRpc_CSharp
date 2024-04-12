using JetBrains.Annotations;

namespace PlayifyRpc.Types.Exceptions;

[AttributeUsage(AttributeTargets.Class)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
[MeansImplicitUse]
[BaseTypeRequired(typeof(RpcException))]
public class RpcCustomExceptionAttribute:Attribute{
	internal readonly string TypeTag;

	public RpcCustomExceptionAttribute(string typeTag)=>TypeTag=typeTag;
}