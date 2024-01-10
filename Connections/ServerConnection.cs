using System.Text;
using PlayifyRpc.Internal;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Connections;

public abstract class ServerConnection:AnyConnection,IAsyncDisposable{
	internal static readonly HashSet<ServerConnection> Connections=new();
	private readonly Dictionary<int,(ServerConnection respondFrom,int respondId)> _activeExecutions=new();
	private readonly Dictionary<int,(ServerConnection respondTo,int respondId)> _activeRequests=new();
	internal readonly HashSet<string> Types=new();
	private readonly ServerInvoker _invoker;

	private int _nextId;


	protected ServerConnection(){
		_invoker=new ServerInvoker(this);
		lock(Connections) Connections.Add(this);
	}

	#region Connection
	public virtual async ValueTask DisposeAsync(){
		GC.SuppressFinalize(this);

		lock(Connections) Connections.Remove(this);

		lock(RpcServer.Types){
			foreach(var type in Types)
				if(RpcServer.Types.Remove(type,out var con)&&con!=this)
					RpcServer.Types[type]=con;//if deleted wrongly, put back in
			Types.Clear();
		}


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
		var exception=new RpcException("RpcExcetion","SERVER","Connection closed","");
		await Task.WhenAll(toReject.Select(t=>t.respondTo.Reject(t.respondId,exception))
		                           .Concat(toCancel.Select(t=>t.respondTo.CancelRaw(t.respondId,null))));
	}

	protected override void RespondedToCallId(int callId){
		lock(_activeExecutions) _activeExecutions.Remove(callId);
	}


	protected override async Task Receive(DataInputBuff data){
		var packetType=(PacketType)data.ReadByte();
		switch(packetType){
			case PacketType.FunctionCall:{
				var callId=data.ReadLength();
				var type=data.ReadString();
				if(type==null) await CallServer(this,data,callId);
				else{
					ServerConnection? handler;
					lock(RpcServer.Types)
						if(!RpcServer.Types.TryGetValue(type,out handler))
							handler=null;
					if(handler==null) await Reject(callId,new RpcException("RpcException","SERVER","Unknown Type: "+type,""));
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
						Console.WriteLine($"{this} has no ActiveRequest with id: {callId}");
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
						Console.WriteLine($"{this} has no ActiveRequest with id: {callId}");
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
						Console.WriteLine($"{this} has no ActiveExecution with id: {callId}");
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
						Console.WriteLine($"{this} has no ActiveExecution with id: {callId}");
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
						Console.WriteLine($"{this} has no ActiveRequest with id: {callId}");
						break;
					}
				var buff=new DataOutputBuff();
				buff.WriteByte((byte)PacketType.MessageToCaller);
				buff.WriteLength(tuple.respondId);
				buff.Write(data);
				await tuple.respondTo.SendRaw(buff);
				break;
			}
			default:throw new ArgumentOutOfRangeException();
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

	private static async Task CallServer(ServerConnection connection,DataInput data,int callId){
		var method=data.ReadString();

		var already=new List<object>();
		var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();

		try{
			var result=connection._invoker.Invoke(null!,method,args);
			await connection.Resolve(callId,result);
		} catch(Exception e){
			await connection.Reject(callId,e);
		}
	}
	#endregion


	#region Local
	private string? _name;
	public string Name=>_name??ToString();

	internal void SetName(string? s){
		_name=s;

		if(s==null) return;
		ServerConnection[] toKick;
		lock(Connections) toKick=Connections.Where(c=>c!=this&&c.Name==s).ToArray();
		TaskUtils.WhenAll(toKick.Select(k=>{
			Console.WriteLine("Kicking client "+k+" as new client with same name joined.");
			return k.DisposeAsync();
		})).AsTask().TryCatch();
	}

	public override string ToString(){
		var str=new StringBuilder(GetType().Name);
		str.Append('(').Append(GetHashCode().ToString("x8"));
		if(_name!=null) str.Append(':').Append(Name);
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
			if(log) Console.WriteLine($"{Name} tried registering Types \"{types.Join("\",\"")}\"");
			throw new Exception($"Types \"{failed.Join("\",\"")}\" were already registered");
		}
		if(log) Console.WriteLine($"{Name} registered Types \"{types.Join("\",\"")}\"");
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
			if(log) Console.WriteLine($"{Name} tried unregistering Types \"{types.Join("\",\"")}\"");
			throw new Exception($"Types \"{failed.Join("\",\"")}\" were not registered");
		}
		if(log) Console.WriteLine($"{Name} unregistered Types \"{types.Join("\",\"")}\"");
	}
	#endregion
}