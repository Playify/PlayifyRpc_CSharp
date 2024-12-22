using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyRpc.Types;
using PlayifyUtility.Loggers;
#if NETFRAMEWORK
using System.Diagnostics;
#endif

namespace PlayifyRpc;

[PublicAPI]
public static partial class Rpc{
	private static Logger? _logger;
	public static Logger Logger{
		get=>_logger??=new Logger().WithDate().WithName("Rpc");
		set=>_logger=value;
	}

	static Rpc(){
		RpcSetupAttribute.LoadAll();
	}

#if NETFRAMEWORK
	public static readonly string Id=Environment.MachineName+"@"+Process.GetCurrentProcess().Id;
#else
	public static readonly string Id=Environment.MachineName+"@"+Environment.ProcessId;
#endif
	public static string PrettyName=>Name is{} name?$"{name} ({Id})":Id;
	public static string? Name=>RegisteredTypes.Name;

	public static Task SetName(string name)=>RegisteredTypes.SetName(name);
}