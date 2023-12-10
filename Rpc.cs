using JetBrains.Annotations;
using PlayifyRpc.Internal;

namespace PlayifyRpc;

[PublicAPI]
public static partial class Rpc{
#if NETFRAMEWORK
	public static readonly string Id=Environment.MachineName+"@"+System.Diagnostics.Process.GetCurrentProcess().Id;
#else
	public static readonly string Id=Environment.MachineName+"@"+Environment.ProcessId;
#endif
	public static string NameOrId=>RegisteredTypes.Name is{} name?$"{name} ({Id})":Id;

	public static Task SetName(string name)=>RegisteredTypes.SetName(name);
}