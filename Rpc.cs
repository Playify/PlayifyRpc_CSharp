using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyUtility.Loggers;

namespace PlayifyRpc;

[PublicAPI]
public static partial class Rpc{
	private static Logger? _logger;
	public static Logger Logger{
		get=>_logger??=new Logger().WithName("Rpc");
		set=>_logger=value;
	}
	
#if NETFRAMEWORK
	public static readonly string Id=Environment.MachineName+"@"+System.Diagnostics.Process.GetCurrentProcess().Id;
#else
	public static readonly string Id=Environment.MachineName+"@"+Environment.ProcessId;
#endif
	public static string PrettyName=>Name is{} name?$"{name} ({Id})":Id;
	public static string? Name=>RegisteredTypes.Name;

	public static Task SetName(string name)=>RegisteredTypes.SetName(name);
}