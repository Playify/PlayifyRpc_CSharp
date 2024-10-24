using System.Net;
using System.Text;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Loggers;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Connections;

internal abstract class ServerConnection:AnyConnection,IAsyncDisposable{
	internal static readonly HashSet<ServerConnection> Connections=[];
	private readonly Dictionary<int,(ServerConnection respondFrom,int respondId)> _activeExecutions=new();
	private readonly Dictionary<int,(ServerConnection respondTo,int respondId)> _activeRequests=new();
	internal readonly HashSet<string> Types=[];
	private readonly ServerInvoker _invoker;
	private int _nextId;

	public Logger Logger{get;private set;}

	protected ServerConnection(string? id){
		Id=id??"???";
		Logger=Rpc.Logger;
		Name=null;

		_invoker=new ServerInvoker(this);

		if(id!=null){
			ServerConnection[] toKick;
			lock(Connections){
				toKick=Connections.Where(c=>c.Id==id).ToArray();
				Connections.Add(this);
			}

			TaskUtils.WhenAll(toKick.Select(k=>{
				k.Logger.Warning("Kicked, new client with same id joined.");
				return k.DisposeAsync();
			})).AsTask().Background();

			lock(RpcServer.Types){
				RpcServer.Types["$"+id]=this;
				Types.Add("$"+id);
			}
		}
	}

	//Used when the constructor fails
	protected void ForceUnregister(){
		lock(Connections) Connections.Remove(this);

		lock(RpcServer.Types){
			foreach(var type in Types)
				if(RpcServer.Types.Remove(type,out var con)&&con!=this)
					RpcServer.Types[type]=con;//if deleted wrongly, put back in
			Types.Clear();
		}
	}

	#region Connection
	private bool _disposed;

	public virtual async ValueTask DisposeAsync(){
		if(_disposed) return;
		_disposed=true;
		GC.SuppressFinalize(this);

		ForceUnregister();

		(ServerConnection respondTo,int respondId)[] toReject;
		(ServerConnection respondTo,int respondId)[] toCancel;
		lock(_activeRequests){
			toReject=_activeRequests.Values.ToArray();
			_activeRequests.Clear();
		}
		lock(_activeRequests){
			toCancel=_activeExecutions.Values.ToArray();
			_activeExecutions.Clear();
		}
		var exception=new RpcConnectionException("Connection closed by "+PrettyName);
		await Task.WhenAll(toReject.Select(t=>t.respondTo.Reject(t.respondId,exception))
		                           .Concat(toCancel.Select(t=>t.respondTo.CancelRaw(t.respondId,null))));
	}

	protected override void RespondedToCallId(int callId){
		lock(_activeExecutions) _activeExecutions.Remove(callId);
	}

	protected async Task Receive(DataInputBuff data){
		var packetType=(PacketType)data.ReadByte();
		switch(packetType){
			case PacketType.FunctionCall:{
				var callId=data.ReadLength();
				var type=data.ReadString();

				ListenAllCalls.Broadcast(this,type,data);

				if(type==null) await CallServer(this,data,callId);
				else{
					ServerConnection? handler;
					lock(RpcServer.Types)
						if(!RpcServer.Types.TryGetValue(type,out handler))
							handler=null;
					if(handler==null) await Reject(callId,new RpcTypeNotFoundException(type));
					else{
						var task=handler.CallFunction(type,data,this,callId,out var sentId);
						lock(_activeExecutions) _activeExecutions.Add(callId,(handler,sentId));
						await task;
					}
				}
				break;
			}
			case PacketType.FunctionSuccess:{
				var callId=data.ReadLength();
				(ServerConnection respondTo,int respondId) tuple;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out tuple)){
						Logger.Warning($"Invalid State: No ActiveRequest[{callId}] ({packetType})");
						break;
					}
				await tuple.respondTo.ResolveRaw(tuple.respondId,data);
				break;
			}
			case PacketType.FunctionError:{
				var callId=data.ReadLength();
				(ServerConnection respondTo,int respondId) tuple;
				lock(_activeRequests)
					if(!_activeRequests.Remove(callId,out tuple)){
						Logger.Warning($"Invalid State: No ActiveRequest[{callId}] ({packetType})");
						break;
					}
				await tuple.respondTo.RejectRaw(tuple.respondId,data);
				break;
			}
			case PacketType.FunctionCancel:{
				var callId=data.ReadLength();
				(ServerConnection respondTo,int respondId) tuple;
				lock(_activeExecutions)
					if(!_activeExecutions.TryGetValue(callId,out tuple)){
						Logger.Warning($"Invalid State: No ActiveExecution[{callId}] ({packetType})");
						break;
					}
				await tuple.respondTo.CancelRaw(tuple.respondId,data);
				break;
			}
			case PacketType.MessageToExecutor:{
				var callId=data.ReadLength();
				(ServerConnection respondTo,int respondId) tuple;
				lock(_activeExecutions)
					if(!_activeExecutions.TryGetValue(callId,out tuple)){
						Logger.Warning($"Invalid State: No ActiveExecution[{callId}] ({packetType})");
						break;
					}
				var buff=new DataOutputBuff();
				buff.WriteByte((byte)PacketType.MessageToExecutor);
				buff.WriteLength(tuple.respondId);
				buff.Write(data);
				await tuple.respondTo.SendRaw(buff);
				break;
			}
			case PacketType.MessageToCaller:{
				var callId=data.ReadLength();
				(ServerConnection respondTo,int respondId) tuple;
				lock(_activeRequests)
					if(!_activeRequests.TryGetValue(callId,out tuple)){
						Logger.Warning($"Invalid State: No ActiveRequest[{callId}] ({packetType})");
						break;
					}
				var buff=new DataOutputBuff();
				buff.WriteByte((byte)PacketType.MessageToCaller);
				buff.WriteLength(tuple.respondId);
				buff.Write(data);
				await tuple.respondTo.SendRaw(buff);
				break;
			}
			default:throw new ProtocolViolationException("Received invalid rpc-packet");
		}
	}
	#endregion


	#region Calling functions
	private Task CallFunction(string type,DataInputBuff data,ServerConnection respondTo,int respondId,out int callId){
		var buff=new DataOutputBuff();
		buff.WriteByte((byte)PacketType.FunctionCall);
		callId=Interlocked.Increment(ref _nextId);
		buff.WriteLength(callId);
		buff.WriteString(type);
		buff.Write(data);


		lock(_activeRequests) _activeRequests.Add(callId,(respondTo,respondId));

		return SendRaw(buff);
	}

	private static async Task CallServer(ServerConnection connection,DataInputBuff data,int callId){
		try{
			var method=data.ReadString();

			var args=RpcDataPrimitive.ReadArray(data);

			try{
				var obj=await FunctionCallContext.RunWithContextAsync(()=>connection._invoker.Invoke(null!,method,args),null!,null,method,args);
				var result=RpcDataPrimitive.From(obj);
				await connection.Resolve(callId,result);
			} catch(Exception e){
				await connection.Reject(callId,e);
			}
		} catch(Exception e){
			await connection.Reject(callId,new RpcDataException($"Error reading binary stream ({nameof(CallServer)})",e));
		}
	}
	#endregion


	#region Local
	public readonly string Id;
	private string? _name;
	public string? Name{
		get=>_name;
		internal set{
			_name=value;
			Logger=Rpc.Logger.WithName("Connection: "+PrettyName);
		}
	}
	public string PrettyName=>Name is{} name?$"{name} ({Id})":Id;


	public override string ToString(){
		var str=new StringBuilder(GetType().Name);
		str.Append('(').Append(GetHashCode().ToString("x8"));
		str.Append(':').Append(PrettyName);
		str.Append(')');
		return str.ToString();
	}

	internal void Register(string[] types,bool log){
		if(types.Length==0) return;
		string[] failed;
		lock(RpcServer.Types)
			failed=types.Where(type=>{
				if(!RpcServer.Types.TryAdd(type,this)) return true;
				Types.Add(type);
				return false;
			}).ToArray();

		if(failed.Length!=0){
			if(log)
				Logger.Warning(types.Length==1
					               ?$"Tried registering Type \"{types[0]}\""
					               :$"Tried registering Types \"{types.Join("\",\"")}\"");
			throw new Exception(failed.Length==1
				                    ?$"Type \"{failed[0]}\" was already registered"
				                    :$"Types \"{failed.Join("\",\"")}\" were already registered");
		}
		if(log)
			Logger.Info(types.Length==1
				            ?$"Registered Type \"{types[0]}\""
				            :$"Registered Types \"{types.Join("\",\"")}\"");
	}

	internal void Unregister(string[] types,bool log){
		if(types.Length==0) return;
		string[] failed;
		lock(RpcServer.Types)
			failed=types.Where(type=>{
				if(!Types.Remove(type)) return true;
				RpcServer.Types.Remove(type);
				return false;
			}).ToArray();

		if(failed.Length!=0){
			if(log)
				Logger.Warning(types.Length==1
					               ?$"Tried unregistering Type \"{types[0]}\""
					               :$"Tried unregistering Types \"{types.Join("\",\"")}\"");
			throw new Exception(failed.Length==1
				                    ?$"Type \"{failed[0]}\" was not registered"
				                    :$"Types \"{failed.Join("\",\"")}\" were not registered");
		}
		if(log)
			Logger.Info(types.Length==1
				            ?$"Unregistered Type \"{types[0]}\""
				            :$"Unregistered Types \"{types.Join("\",\"")}\"");
	}
	#endregion

	public string GetCaller(int callId){
		lock(_activeRequests)
			if(_activeRequests.TryGetValue(callId,out var tuple))
				return tuple.respondTo.PrettyName;
		throw new Exception("Error finding caller");
	}
}