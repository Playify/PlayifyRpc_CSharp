using JetBrains.Annotations;

namespace PlayifyRpc.Types;

[PublicAPI]
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Method)]
public class RpcHiddenAttribute:Attribute;