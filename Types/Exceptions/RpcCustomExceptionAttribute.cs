using JetBrains.Annotations;

namespace PlayifyRpc.Types.Exceptions;

[AttributeUsage(AttributeTargets.Class)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
[MeansImplicitUse]
[BaseTypeRequired(typeof(RpcException))]
public class RpcCustomExceptionAttribute(string typeTag):Attribute{
	internal readonly string TypeTag=typeTag;

}