﻿using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Functions;

public delegate void MessageFunc(params object?[] args);

public class FunctionCallContext:SendReceive{
	private readonly MessageFunc _send;
	private readonly TaskCompletionSource<object?> _tcs;
	private readonly CancellationTokenSource _cts=new();

	[PublicAPI]
	public readonly string? Type;
	[PublicAPI]
	public readonly string? Method;
	
	public override bool Finished=>_tcs.Task.IsCompleted;
	public override Task<object?> Task=>_tcs.Task;
	
	[PublicAPI]
	public CancellationToken CancellationToken=>_cts.Token;
	public void Cancel()=>_cts.Cancel();


	public override void SendMessage(params object?[] args)=>_send(args);

	internal FunctionCallContext(string? type,string? method,MessageFunc send,TaskCompletionSource<object?> tcs){
		Type=type;
		Method=method;
		_send=send;
		_tcs=tcs;
	}


	private static int _nextId;

	internal static PendingCall CallFunction(string? type,string method,object?[] args){
		if(type!=null){
			Invoker? local;
			lock(RegisteredTypes.Registered)
				if(!RegisteredTypes.Registered.TryGetValue(type,out local))
					local=null;
			if(local!=null) return local.Call(type,method,args);
		}

		var truth=new PendingCallRawData();

		var call=new PendingCall(truth);
		
		var already=new List<object>();
		call.Finally(()=>{
			foreach(var d in already.OfType<Delegate>()) RpcFunction.UnregisterFunction(d);
		});

		var buff=new DataOutputBuff();
		int callId;
		try{
			buff.WriteByte((byte)PacketType.FunctionCall);
			callId=Interlocked.Increment(ref _nextId);
			buff.WriteLength(callId);
			buff.WriteString(type);
			buff.WriteString(method);
			buff.WriteArray(args,buff.WriteDynamic,already);
		} catch(Exception e){
			call.Reject(e);
			return call;
		}

		var connection=ClientConnection.Instance;
		if(connection!=null){
			truth.SendFunc=msgArgs=>{
				if(truth.Finished)return;
				var msg=new DataOutputBuff();
				msg.WriteByte((byte)PacketType.MessageToExecutor);
				msg.WriteLength(callId);
				var list=new List<object>();
				msg.WriteArray(msgArgs,msg.WriteDynamic,list);
				already.AddRange(list);

				connection.SendRaw(msg);
			};
			truth.CancelFunc=()=>{
				if(truth.Finished)return;
				var msg=new DataOutputBuff();
				msg.WriteByte((byte)PacketType.FunctionCancel);
				msg.WriteLength(callId);
				
				connection.SendRaw(msg);
			};
			
			connection.SendCall(callId,call,buff);
		}
		else call.Reject(new Exception("Not connected"));

		return call;
	}

	private static readonly ThreadLocal<FunctionCallContext?> ThreadLocal=new();

	internal static T RunWithContext<T>(Func<T> func,FunctionCallContext context){
		ThreadLocal.Value=context;
		try{
			return func();
		} finally{
			ThreadLocal.Value=null;
		}
	}

	public static FunctionCallContext GetContext()=>ThreadLocal.Value??throw new InvalidOperationException("FunctionCallContext not available");
}