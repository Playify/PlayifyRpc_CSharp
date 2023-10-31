using System.Collections.Specialized;
using PlayifyRpc.Connections;
using PlayifyUtility.Utils;

namespace PlayifyRpc;


public static partial class Rpc{
	public static Task WaitUntilConnected=>ClientConnection.WaitUntilConnected;
	public static bool IsConnected=>ClientConnection.Instance!=null;
	
	public static void ConnectLoopback(){
		_=ServerConnectionLoopbackClient.Connect().Catch(Console.Error.WriteLine);
	}
	
	
	public static void Connect(Uri uri)=>Connect(uri,Environment.GetEnvironmentVariable("RPC_TOKEN"));
	public static void Connect(Uri uri,string? token)=>Connect(uri,token==null?null:new NameValueCollection{
		{"Cookie","RPC_TOKEN="+token},
	});
	public static void Connect(Uri uri,NameValueCollection? headers){
		_=ClientConnectionWebSocket.Connect(uri,headers).Catch(Console.Error.WriteLine);
	}
}