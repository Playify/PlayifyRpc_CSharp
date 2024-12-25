using System.Net;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Loggers;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Connections;

internal abstract class ClientConnection:AnyConnection,IAsyncDisposable{
	protected static Logger Logger=>Rpc.Logger.WithName("Connection");

	#region Connecting
	internal static ClientConnection? Instance{get;private set;}
	private static TaskCompletionSource? _connectionAttempt;
	private static TaskCompletionSource? _connectionAttemptOnce;
	internal static Task WaitUntilConnectedOnce=>_connectionAttemptOnce?.Task??Task.FromException(new RpcConnectionException("Not yet started to connect"));
	internal static Task WaitUntilConnectedLooping=>(_connectionAttempt??=new TaskCompletionSource()).Task;


	protected static bool IsConnecting()=>_connectionAttemptOnce!=null;
	protected static void StartConnect()=>_connectionAttemptOnce=new TaskCompletionSource();

	protected static async Task DoConnect(ClientConnection connection,string? reportedName=null,HashSet<string>? reportedTypes=null){
		HashSet<string> toRegister;
		lock(RegisteredTypes.Registered) toRegister=RegisteredTypes.Registered.Keys.ToHashSet();
		var toDelete=reportedTypes??["$"+Rpc.Id];
		toRegister.RemoveWhere(toDelete.Remove);

		Instance=connection;

		if(toRegister.Count!=0||toDelete.Count!=0){
			if(Rpc.Name!=reportedName) await Invoker.CallFunction(null,"H",Rpc.Name,toRegister.ToArray(),toDelete.ToArray());
			else await Invoker.CallFunction(null,"H",toRegister.ToArray(),toDelete.ToArray());
		} else if(Rpc.Name!=reportedName) await Invoker.CallFunction(null,"H",Rpc.Name);


		Rpc.IsConnected=true;
		(_connectionAttemptOnce??=new TaskCompletionSource()).TrySetResult();
		(_connectionAttempt??=new TaskCompletionSource()).TrySetResult();
	}

	protected static void FailConnect(Exception e){
		Instance=null;
		Rpc.IsConnected=false;
		Logger.Error("Error connecting to RPC: "+e);
		var tcs=_connectionAttemptOnce;
		_connectionAttemptOnce=new TaskCompletionSource();
		tcs?.TrySetException(e);

		//If already was connected, then start a new wait loop
		if(_connectionAttempt?.Task.IsCompleted??false) _connectionAttempt=null;
	}
	#endregion

	private readonly Dictionary<int,PendingCallRawData> _activeRequests=new();
	private readonly Dictionary<int,FunctionCallContext> _currentlyExecuting=new();

	internal void SendCall(int callId,PendingCallRawData call,DataOutputBuff buff){
		lock(_activeRequests) _activeRequests.Add(callId,call);
		SendRaw(buff).Catch(call.Reject);
	}

	protected override void RespondedToCallId(int callId){
		lock(_currentlyExecuting) _currentlyExecuting.Remove(callId);
	}

	protected async Task Receive(DataInputBuff data){
		var packetType=(PacketType)data.ReadByte();
		switch(packetType){
			case PacketType.FunctionCall:{
				var callId=data.ReadLength();


				var tcs=new TaskCompletionSource<RpcDataPrimitive>();
				try{
					var type=data.ReadString();

					if(type==null)
						throw new RpcTypeNotFoundException(null);
					Invoker? local;
					lock(RegisteredTypes.Registered)
						if(!RegisteredTypes.Registered.TryGetValue(type,out local))
							throw new RpcTypeNotFoundException(type);

					var method=data.ReadString();

					var args=RpcDataPrimitive.ReadArray(data);


					var context=new FunctionCallContext(type,
						method,
						args,
						sending=>{
							if(tcs.Task.IsCompleted) return;
							var msg=new DataOutputBuff();
							msg.WriteByte((byte)PacketType.MessageToCaller);
							msg.WriteLength(callId);
							var already=new Dictionary<RpcDataPrimitive,int>();
							msg.WriteArray(sending,d=>d.Write(msg,already));
							SendRaw(msg);
						},
						tcs,
						()=>Invoker.CallFunction<string>(null,"c",callId));
					lock(_currentlyExecuting) _currentlyExecuting.Add(callId,context);

					try{
						var result=await Invoker.RunAndAwait(ctx=>local.Invoke(type,method,args,ctx),context,type,method,args);
						tcs.TrySetResult(result);
						await Resolve(callId,result);
					} catch(Exception e){//Inner catch handles normal errors, outer catch handles data exceptions
						tcs.TrySetException(e);
						await Reject(callId,e);
					}
				} catch(TypeInitializationException e){
					Rpc.Logger.Critical("Error Initializing Type while receiving RPC: "+e);
					tcs.TrySetException(e);
					await Reject(callId,e);
				} catch(RpcException e){
					tcs.TrySetException(e);
					await Reject(callId,e);
				} catch(Exception cause){
					var e=new RpcDataException($"Error reading binary stream ({packetType})",cause);
					tcs.TrySetException(e);
					await Reject(callId,e);
				}

				break;
			}
			case PacketType.FunctionSuccess:{
				var callId=data.ReadLength();
				PendingCallRawData? pending;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out pending)){
						Logger.Warning($"Invalid State: No ActiveRequest[{callId}] ({packetType})");
						break;
					}
				try{
					var already=new Dictionary<int,RpcDataPrimitive>();
					pending.Resolve(RpcDataPrimitive.Read(data,already));
				} catch(Exception e){
					pending.Reject(new RpcDataException($"Error reading binary stream ({packetType})",e));
				}
				break;
			}
			case PacketType.FunctionError:{
				var callId=data.ReadLength();
				PendingCallRawData? pending;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out pending)){
						Logger.Warning($"Invalid State: No ActiveRequest[{callId}] ({packetType})");
						break;
					}
				try{
					pending.Reject(RpcException.Read(data));
				} catch(Exception e){
					pending.Reject(new RpcDataException($"Error reading binary stream ({packetType})",e));
				}
				break;
			}
			case PacketType.FunctionCancel:{
				var callId=data.ReadLength();
				FunctionCallContext? ctx;
				lock(_currentlyExecuting)
					if(!_currentlyExecuting.TryGetValue(callId,out ctx)){
						//Already finished executing => no longer in list. Would mean additional overhead to prevent this warning, so just not warning here should be fine
						//Logger.Warning($"Invalid State: No CurrentlyExecuting[{callId}] ({packetType})");
						break;
					}
				ctx.CancelSelf();
				break;
			}
			case PacketType.MessageToExecutor:{
				var callId=data.ReadLength();
				FunctionCallContext? ctx;
				lock(_currentlyExecuting)
					if(!_currentlyExecuting.TryGetValue(callId,out ctx)){
						Logger.Warning($"Invalid State: No CurrentlyExecuting[{callId}] ({packetType})");
						break;
					}
				ctx.MessageQueue.DoReceiveMessage(RpcDataPrimitive.ReadArray(data));
				break;
			}
			case PacketType.MessageToCaller:{
				var callId=data.ReadLength();
				PendingCallRawData? pending;
				lock(_activeRequests)
					if(!_activeRequests.TryGetValue(callId,out pending)){
						Logger.Warning($"Invalid State: No ActiveRequest[{callId}] ({packetType})");
						break;
					}
				pending.MessageQueue.DoReceiveMessage(RpcDataPrimitive.ReadArray(data));
				break;
			}
			default:throw new ProtocolViolationException("Received invalid rpc-packet");
		}
	}

	private bool _disposed;

	public virtual ValueTask DisposeAsync(){
		if(_disposed) return default;
		_disposed=true;
		GC.SuppressFinalize(this);

		lock(_activeRequests)
			if(_activeRequests.Count!=0){
				var exception=new RpcConnectionException("Websocket closed");
				foreach(var pending in _activeRequests.Values) pending.Reject(exception);
				_activeRequests.Clear();
			}
		lock(_currentlyExecuting)
			if(_currentlyExecuting.Count!=0)
				foreach(var ctx in _currentlyExecuting.Values)
					ctx.CancelSelf();
		return default;
	}
}