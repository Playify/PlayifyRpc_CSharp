using PlayifyRpc.Internal;

namespace PlayifyRpc;

public static partial class Rpc{
	public static readonly string Id=Environment.MachineName+"@"+Environment.ProcessId;
	public static string NameOrId=>RegisteredTypes.Name is {} name?$"{name} ({Id})":Id;
}