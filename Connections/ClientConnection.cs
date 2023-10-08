﻿using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils;

namespace PlayifyRpc.Connections;

internal abstract class ClientConnection:AnyConnection,IAsyncDisposable{
	internal static ClientConnection? Instance{get;private set;}
	private static TaskCompletionSource? _tcs;
	internal static Task WaitUntilConnected=>_tcs?.Task??Task.FromException(new Exception("Not yet started"));

	private readonly Dictionary<int,PendingCall> _activeRequests=new();
	private readonly Dictionary<int,FunctionCallContext> _currentlyExecuting=new();


	protected static void StartConnect(bool reconnect){
		if(!reconnect&&_tcs!=null) throw new Exception("Already connected");
		_tcs=new TaskCompletionSource();
	}

	protected static async Task DoConnect(ClientConnection connection){
		Instance=connection;
		
		await Rpc.CallFunction(null,"N",Rpc.NameOrId);
		object?[] types;
		lock(RegisteredTypes.Registered) types=RegisteredTypes.Registered.Keys.Cast<object?>().ToArray();
		await Rpc.CallFunction(null,"+",types);

		(_tcs??=new TaskCompletionSource()).TrySetResult();
	}

	protected static void FailConnect(Exception e){
		Instance=null;
		Console.WriteLine(e);
		var tcs=_tcs;
		_tcs=new TaskCompletionSource();
		tcs?.TrySetException(e);
	}
	

	internal void SendCall(int callId,PendingCall call,DataOutputBuff buff){
		lock(_activeRequests) _activeRequests.Add(callId,call);
		SendRaw(buff).Catch(call.Reject);
	}

	protected override void RespondedToCallId(int callId){
		lock(_currentlyExecuting) _currentlyExecuting.Remove(callId);
	}

	protected override async Task Receive(DataInputBuff data){
		var packetType=(PacketType)data.ReadByte();
		switch(packetType){
			case PacketType.FunctionCall:{
				var callId=data.ReadLength();


				var already=new List<object>();
				var tcs=new TaskCompletionSource<object?>();
				try{
					var type=data.ReadString();
					
					if(type==null) throw new Exception("Client can't use null as a type for function calls");
					Invoker? local;
					lock(RegisteredTypes.Registered)
						if(!RegisteredTypes.Registered.TryGetValue(type,out local))
							throw new Exception($"Type \"{type}\" is not registered on client {Rpc.NameOrId}");

					var method=data.ReadString();

					var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();


					var context=new FunctionCallContext(type,
					                                    method!,
					                                    sending=>{
						                                    if(tcs.Task.IsCompleted) return;
						                                    var msg=new DataOutputBuff();
						                                    msg.WriteByte((byte)PacketType.MessageToCaller);
						                                    msg.WriteLength(callId);
						                                    var list=new List<object>();
						                                    msg.WriteArray(sending,msg.WriteDynamic,list);
						                                    already.AddRange(list);

						                                    SendRaw(msg);
					                                    },
					                                    tcs);
					lock(_currentlyExecuting) _currentlyExecuting.Add(callId,context);

					var invokeResult=FunctionCallContext.RunWithContext(()=>local.DynamicInvoke(type,method!,args),context);
					var result=await StaticallyTypedUtils.UnwrapTask(invokeResult);
					tcs.TrySetResult(result);
					await Resolve(callId,result);
				} catch(Exception e){
					tcs.TrySetException(e);
					await Reject(callId,e);
				} finally{
					foreach(var d in already.OfType<Delegate>()) RpcFunction.UnregisterFunction(d);
				}

				break;
			}
			case PacketType.FunctionSuccess:{
				var callId=data.ReadLength();
				PendingCall? pending;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out pending)){
						Console.WriteLine($"{Rpc.NameOrId} has no ActiveRequest with id: {callId}");
						break;
					}
				try{
					pending.Resolve(data.ReadDynamic());
				} catch(Exception e){
					pending.Reject(e);
				}
				break;
			}
			case PacketType.FunctionError:{
				var callId=data.ReadLength();
				PendingCall? pending;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out pending)){
						Console.WriteLine($"{Rpc.NameOrId} has no ActiveRequest with id: {callId}");
						break;
					}
				try{
					pending.Reject(data.ReadException());
				} catch(Exception e){
					pending.Reject(e);
				}
				break;
			}
			case PacketType.FunctionCancel:{
				var callId=data.ReadLength();
				FunctionCallContext? ctx;
				lock(_currentlyExecuting)
					if(!_currentlyExecuting.TryGetValue(callId,out ctx)){
						Console.WriteLine($"{Rpc.NameOrId} has no CurrentlyExecuting with id: {callId}");
						break;
					}
				ctx.Cancel();
				break;
			}
			case PacketType.MessageToExecutor:{
				var callId=data.ReadLength();
				FunctionCallContext? ctx;
				lock(_currentlyExecuting)
					if(!_currentlyExecuting.TryGetValue(callId,out ctx)){
						Console.WriteLine($"{Rpc.NameOrId} has no CurrentlyExecuting with id: {callId}");
						break;
					}
				var already=new List<object>();
				var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();
				ctx.DoReceiveMessage(args);
				break;
			}
			case PacketType.MessageToCaller:{
				var callId=data.ReadLength();
				PendingCall? pending;
				lock(_activeRequests)
					if(!_activeRequests.TryGetValue(callId,out pending)){
						Console.WriteLine($"{Rpc.NameOrId} has no ActiveRequest with id: {callId}");
						break;
					}
				var already=new List<object>();
				var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();

				pending.DoReceiveMessage(args);
				break;
			}
			default:throw new ArgumentOutOfRangeException();
		}
	}

	public virtual ValueTask DisposeAsync(){
		GC.SuppressFinalize(this);

		lock(_activeRequests)
			if(_activeRequests.Count!=0){
				var exception=new Exception("Websocket closed");
				foreach(var pending in _activeRequests.Values) pending.Reject(exception);
				_activeRequests.Clear();
			}
		lock(_currentlyExecuting)
			if(_currentlyExecuting.Count!=0){
				foreach(var ctx in _currentlyExecuting.Values) ctx.Cancel();
			}
		return default;
	}
}