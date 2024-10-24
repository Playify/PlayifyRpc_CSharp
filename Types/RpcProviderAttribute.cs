using JetBrains.Annotations;

namespace PlayifyRpc.Types;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
public class RpcProviderAttribute(string? type=null):Attribute{
	internal readonly string? Type=type;

}