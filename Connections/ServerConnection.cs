using System.Text;
using PlayifyRpc.Internal;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Connections;

public abstract class ServerConnection:AnyConnection,IAsyncDisposable{
	internal static readonly HashSet<ServerConnection> Connections=new();
	private readonly Dictionary<int,(ServerConnection respondFrom,int respondId)> _activeExecutions=new();
	private readonly Dictionary<int,(ServerConnection respondTo,int respondId)> _activeRequests=new();
	private readonly HashSet<string> _types=new();
	private string? _name;

	private int _nextId;


	protected ServerConnection(){
		lock(Connections) Connections.Add(this);
	}

	public virtual async ValueTask DisposeAsync(){
		GC.SuppressFinalize(this);

		lock(Connections) Connections.Remove(this);

		lock(RpcServer.Types){
			foreach(var type in _types)
				if(RpcServer.Types.Remove(type,out var con)&&con!=this)
					RpcServer.Types[type]=con;//if deleted wrongly, put back in
			_types.Clear();
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
		await Task.WhenAll(
		                   toReject.Select(t=>t.respondTo.Reject(t.respondId,exception))
		                           .Concat(toCancel.Select(t=>t.respondTo.CancelRaw(t.respondId,null)))
		                  );
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
		var method=data.ReadString()??throw new InvalidOperationException();

		var already=new List<object>();
		var args=data.ReadArray(data.ReadDynamic,already)??Array.Empty<object?>();

		object? result=null;

		try{
			switch(method){
				case "+":{
					object?[] failed;
					lock(RpcServer.Types)
						failed=args.Where(typeObj=>{
							if(typeObj is not string type) return true;
							if(!RpcServer.Types.TryAdd(type,connection)) return true;
							connection._types.Add(type);
							return false;
						}).ToArray();
					if(failed.Length!=0){
						Console.WriteLine($"{connection} tried registering Types \"{args.Join("\",\"")}\"");
						throw new Exception($"Types \"{failed.Join("\",\"")}\" were already registered");
					}
					Console.WriteLine($"{connection} registered Types \"{args.Join("\",\"")}\"");
					break;
				}
				case "-":{
					object?[] failed;
					lock(RpcServer.Types)
						failed=args.Where(typeObj=>{
							if(typeObj is not string type) return true;
							if(!connection._types.Remove(type)) return true;
							RpcServer.Types.Remove(type);
							return false;
						}).ToArray();
					if(failed.Length!=0){
						Console.WriteLine($"{connection} tried unregistering Types \"{args.Join("\",\"")}\"");
						throw new Exception($"Types \"{failed.Join("\",\"")}\" were not registered");
					}
					Console.WriteLine($"{connection} unregistered Types \"{args.Join("\",\"")}\"");
					break;
				}
				case "?":{
					result=RpcServer.CheckTypes(args.OfType<string>().ToArray());
					break;
				}
				case "O":{
					result=RpcServer.GetObjectWithFallback(args.OfType<string>().ToArray());
					break;
				}
				case "T":{
					result=RpcServer.GetAllTypes();
					break;
				}
				case "C":{
					result=RpcServer.GetAllConnections();
					break;
				}
				case "N":{
					connection._name=args.Length==0?null:(string?)args[0];
					break;
				}
				default:
					throw new Exception("Unknown server method: "+method);
			}

			await connection.Resolve(callId,result);
		} catch(Exception e){
			await connection.Reject(callId,e);
		}
	}


	public override string ToString(){
		var str=new StringBuilder(GetType().Name);
		str.Append('(').Append(GetHashCode().ToString("x8"));
		if(_name!=null) str.Append(':').Append(_name);
		str.Append(')');
		return str.ToString();
	}
}