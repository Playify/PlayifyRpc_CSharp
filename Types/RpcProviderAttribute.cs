using JetBrains.Annotations;

namespace PlayifyRpc.Types;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
public class RpcProviderAttribute:Attribute{
	internal readonly string? Type;

	public RpcProviderAttribute(string? type=null)=>Type=type;
}