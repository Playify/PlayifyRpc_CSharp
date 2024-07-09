using System.Net;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Connections;

internal abstract class ClientConnection:AnyConnection,IAsyncDisposable{
	internal static ClientConnection? Instance{get;private set;}
	private static TaskCompletionSource? _tcs;
	private static TaskCompletionSource? _tcsOnce;
	internal static Task WaitUntilConnectedOnce=>_tcsOnce?.Task??Task.FromException(new RpcConnectionException("Not yet started to connect"));

	internal static Task WaitUntilConnectedLooping=>(_tcs??=new TaskCompletionSource()).Task;

	private readonly Dictionary<int,PendingCall> _activeRequests=new();
	private readonly Dictionary<int,FunctionCallContext> _currentlyExecuting=new();


	protected static void StartConnect(bool reconnect){
		if(!reconnect&&_tcsOnce!=null) throw new RpcConnectionException("Already connected");
		_tcsOnce=new TaskCompletionSource();
	}

	protected static async Task DoConnect(ClientConnection connection,string? reportedName=null,HashSet<string>? reportedTypes=null){
		HashSet<string> toRegister;
		lock(RegisteredTypes.Registered) toRegister=RegisteredTypes.Registered.Keys.ToHashSet();
		var toDelete=reportedTypes??new HashSet<string>{"$"+Rpc.Id};
		toRegister.RemoveWhere(toDelete.Remove);

		Instance=connection;

		if(toRegister.Count!=0||toDelete.Count!=0){
			if(Rpc.Name!=reportedName) await FunctionCallContext.CallFunction(null,"H",Rpc.Name,toRegister.ToArray(),toDelete.ToArray());
			else await FunctionCallContext.CallFunction(null,"H",toRegister.ToArray(),toDelete.ToArray());
		} else if(Rpc.Name!=reportedName) await FunctionCallContext.CallFunction(null,"H",Rpc.Name);


		Rpc.IsConnected=true;
		(_tcsOnce??=new TaskCompletionSource()).TrySetResult();
		(_tcs??=new TaskCompletionSource()).TrySetResult();
	}

	protected static void FailConnect(Exception e){
		Instance=null;
		Rpc.IsConnected=false;
		Console.WriteLine("Error connecting to RPC: "+e);
		var tcs=_tcsOnce;
		_tcsOnce=new TaskCompletionSource();
		tcs?.TrySetException(e);

		//If already was connected, then start a new wait loop
		if(_tcs?.Task.IsCompleted??false) _tcs=null;
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

					if(type==null)
						throw new RpcTypeNotFoundException(null);
					Invoker? local;
					lock(RegisteredTypes.Registered)
						if(!RegisteredTypes.Registered.TryGetValue(type,out local))
							throw new RpcTypeNotFoundException(type);

					var method=data.ReadString();

					var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();
					DynamicData.CleanupBeforeFreeing(already);


					var context=new FunctionCallContext(type,
						method,
						sending=>{
							if(tcs.Task.IsCompleted) return;
							var msg=new DataOutputBuff();
							msg.WriteByte((byte)PacketType.MessageToCaller);
							msg.WriteLength(callId);
							var list=new List<object>();
							msg.WriteArray(sending,msg.WriteDynamic,list);
							lock(already)
								already.AddRange(list.Where(DynamicData.NeedsFreeing));

							SendRaw(msg);
						},
						tcs,
						()=>FunctionCallContext.CallFunction<string>(null,"c",callId));
					lock(_currentlyExecuting) _currentlyExecuting.Add(callId,context);

					var result=await FunctionCallContext.RunWithContextAsync(()=>local.Invoke(type,method,args),context,type,method,args);
					tcs.TrySetResult(result);
					await Resolve(callId,result);
				} catch(Exception e){
					if(e is TypeInitializationException)
						Console.WriteLine("Error Initializing Type while receiving RPC: "+e);
					tcs.TrySetException(e);
					await Reject(callId,e);
				} finally{
					DynamicData.Free(already);
				}

				break;
			}
			case PacketType.FunctionSuccess:{
				var callId=data.ReadLength();
				PendingCall? pending;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out pending)){
						Console.WriteLine($"{Rpc.PrettyName} has no ActiveRequest with id: {callId}");
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
						Console.WriteLine($"{Rpc.PrettyName} has no ActiveRequest with id: {callId}");
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
						Console.WriteLine($"{Rpc.PrettyName} has no CurrentlyExecuting with id: {callId}");
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
						Console.WriteLine($"{Rpc.PrettyName} has no CurrentlyExecuting with id: {callId}");
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
						Console.WriteLine($"{Rpc.PrettyName} has no ActiveRequest with id: {callId}");
						break;
					}
				var already=new List<object>();
				var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();

				pending.DoReceiveMessage(args);
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