using PlayifyRpc.Connections;

namespace PlayifyRpc.Internal;

public static class RpcServerTypes{
	internal static readonly Dictionary<string,ServerConnection> Types=new();
	
	public static void Disconnect(ServerConnection connection){
		lock(Types){
			foreach(var type in connection.Types)
				if(Types.Remove(type,out var con)&&con!=connection)
					Types[type]=con;//if deleted wrongly, put back in
			connection.Types.Clear();
		}
	}

	public static ServerConnection? GetConnectionForType(string type){
		lock(Types) return Types.TryGetValue(type,out var handler)?handler:null;
	}

	public static bool HasType(string? type){
		if(type==null) return false;
		lock(Types) return Types.ContainsKey(type);
	}

	public static string[] GetAllTypes(){
		lock(Types) return Types.Keys.ToArray();
	}
	
	public static string[] GetAllConnections(){
		lock(ServerConnection.Connections) return ServerConnection.Connections.Select(c=>c.ToString()).ToArray();
	}
}