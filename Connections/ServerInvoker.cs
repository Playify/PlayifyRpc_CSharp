using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Loggers;

namespace PlayifyRpc.Connections;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal class ServerInvoker(ServerConnection connection):TypeInvoker{

	[RpcNamed("N")]//Rpc.SetName
	public void Name(string? name)=>connection.Name=name;

	[RpcNamed("H")]//Connections
	public void Handshake(string? name)=>Name(name);

	[RpcNamed("H")]
	public void Handshake(string? name,string[]? register,string[]? unregister){
		Name(name);
		Handshake(register,unregister);
	}

	[RpcNamed("H")]
	public void Handshake(string[]? register,string[]? unregister){
		if(register!=null) connection.Register(register,false);
		if(unregister!=null) connection.Unregister(unregister,false);
	}

	[RpcNamed("+")]//Rpc.RegisterType
	public void Register(params string[] types)=>connection.Register(types,true);

	[RpcNamed("-")]//Rpc.UnregisterType
	public void Unregister(params string[] types)=>connection.Unregister(types,true);

	[RpcNamed("E")]//RpcObject.Exists
	public static bool CheckType(string type)=>RpcServer.CheckType(type);

	[RpcNamed("c")]//FunctionCallContext.GetCaller
	public string GetCaller(int callId)=>connection.GetCaller(callId);

	[RpcNamed("L")]//ServerLogger.LogLevel
	public void LogLevel(Logger.LogLevel level,params RpcDataPrimitive[] args)=>connection.Logger.Log(level,RpcLogger.ReceiveLog(connection.PrettyName,level,args));
}