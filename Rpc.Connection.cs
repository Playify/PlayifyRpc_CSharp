using System.Collections.Specialized;
using PlayifyRpc.Connections;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task WaitUntilConnected=>ClientConnection.WaitUntilConnectedLooping;//Will wait even on failed attempts, until connected successfully
	public static Task WaitUntilConnectedOnce=>ClientConnection.WaitUntilConnectedOnce;//Will throw on failed connection attempts
	public static bool IsConnected{get;internal set;}

	public static void ConnectLoopback()=>_=ServerConnectionLoopbackClient.Connect().Catch(Logger.Error);

	public static void Connect()=>Connect(new Uri(Environment.GetEnvironmentVariable("RPC_URL")??throw new ArgumentException("Environment variable RPC_URL is not defined")),Environment.GetEnvironmentVariable("RPC_TOKEN"));

	public static void Connect(Uri uri)=>Connect(uri,Environment.GetEnvironmentVariable("RPC_TOKEN"));

	public static void Connect(Uri uri,string? token)
		=>Connect(uri,
		          token==null
			          ?null
			          :new NameValueCollection{
				          {"Cookie","RPC_TOKEN="+token},
			          });

	public static void Connect(Uri uri,NameValueCollection? headers)=>_=ClientConnectionWebSocket.Connect(uri,headers).Catch(Logger.Error);
}