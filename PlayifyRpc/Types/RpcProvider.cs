using JetBrains.Annotations;

namespace PlayifyRpc.Types;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
//[BaseTypeRequired(typeof(IDynamicType))]
public class RpcProvider:Attribute{
	internal readonly string? Type;

	public RpcProvider(string? type=null)=>Type=type;
}