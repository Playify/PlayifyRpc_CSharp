using System.Diagnostics;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;
using PlayifyRpc.Utils;
using PlayifyUtility.Loggers;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc;

[RpcSetup]
[PublicAPI]
public static class RpcLogger{
	static RpcLogger(){
		RpcData.Register<Logger.LogLevel>(
			(value,_)=>new RpcDataPrimitive(Array.IndexOf(LogLevels,value)),
			(primitive,_)=>primitive.IsNumber(0,LogLevels.Length-1,out var i)?LogLevels[i]:RpcData.ContinueWithNext,
			(_,_)=>"Logger.LogLevel"
		);
	}

	private static readonly Logger.LogLevel[] LogLevels=[
		Logger.LogLevel.Log,
		Logger.LogLevel.Special,
		Logger.LogLevel.Debug,
		Logger.LogLevel.Info,
		Logger.LogLevel.Warning,
		Logger.LogLevel.Error,
		Logger.LogLevel.Critical,
	];

	public static async void LogLevel(Logger.LogLevel level,params object?[] msg){
		using var already=new RpcDataPrimitive.Already();
		var args=RpcDataPrimitive.FromArray(msg,already);
		try{
			await Invoker.CallFunctionRaw(null,"L",[RpcDataPrimitive.From(level,already),..args]);
		} catch(Exception e){
			Rpc.Logger.WithName("RpcLogger(Fallback)").Log(level,MessageFromArray(args));
			System.Diagnostics.Debug.Print("RpcLogger failed to send due to "+e);
		}
	}

	public static void Log(params object?[] msg)=>LogLevel(Logger.LogLevel.Log,msg);
	public static void Special(params object?[] msg)=>LogLevel(Logger.LogLevel.Special,msg);
	public static void Debug(params object?[] msg)=>LogLevel(Logger.LogLevel.Debug,msg);
	public static void Info(params object?[] msg)=>LogLevel(Logger.LogLevel.Info,msg);
	public static void Warning(params object?[] msg)=>LogLevel(Logger.LogLevel.Warning,msg);
	public static void Error(params object?[] msg)=>LogLevel(Logger.LogLevel.Error,msg);
	public static void Critical(params object?[] msg)=>LogLevel(Logger.LogLevel.Critical,msg);

	/// Only prints if Debugger.IsAttached
	public static void Debugging(params object?[] msg){
		if(Debugger.IsAttached) Debug(msg);
	}


	public static PendingCall ListenLogs()=>Rpc.CallFunction("Rpc","ListenLogs");


	#region Internal
	internal static RpcListenerSet LocalListeners=[];

	private static string MessageFromArray(RpcDataPrimitive[] args)
		=>args.All(a=>a.IsString(out _))
			  ?args.Select(a=>a.IsString(out var s)?s:"").Join(" ")
			  :args.Join(' ');

	internal static string ReceiveLog(string? caller,Logger.LogLevel level,RpcDataPrimitive[] args){
		var msg=MessageFromArray(args);
		LocalListeners.SendAll(new StringMap{
			{"caller",caller},
			{"level",level},
			{"msg",msg},
			{"args",args},
		});
		return msg;
	}
	#endregion

}