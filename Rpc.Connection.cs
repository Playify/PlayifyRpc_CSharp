using System.Collections.Specialized;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyUtils.Utils;

namespace PlayifyRpc;


public static partial class Rpc{
	public static Task WaitUntilConnected=>ClientConnection.WaitUntilConnected;
	public static bool IsConnected=>ClientConnection.Instance!=null;
	
	public static void ConnectLoopback(){
		_=ServerConnectionLoopbackClient.Connect().Catch(Console.Error.WriteLine);
	}
	
	
	public static void Connect(Uri uri,string? name=null)=>Connect(uri,null,name);
	public static void Connect(Uri uri,NameValueCollection? headers,string? name=null){
		_=ClientConnectionWebSocket.Connect(uri,headers,name).Catch(Console.Error.WriteLine);
	}

	public static Task SetName(string name)=>RegisteredTypes.SetName(name);
}