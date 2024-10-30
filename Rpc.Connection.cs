using System.Collections.Specialized;
using PlayifyRpc.Connections;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc;

public static partial class Rpc{
	public static Task WaitUntilConnected=>ClientConnection.WaitUntilConnectedLooping;//Will wait even on failed attempts, until connected successfully
	public static Task WaitUntilConnectedOnce=>ClientConnection.WaitUntilConnectedOnce;//Will throw on failed connection attempts
	public static bool IsConnected{get;internal set;}

	public static void Connect()=>Connect((string?)null);

	public static void Connect(Uri uri)=>Connect(null,uri);

	public static void Connect(Uri uri,string? token)=>Connect(null,uri,token);

	public static void Connect(Uri uri,NameValueCollection? headers)=>Connect(null,uri,headers);

	public static void Connect(string? name)=>Connect(name,new Uri(Environment.GetEnvironmentVariable("RPC_URL")??throw new ArgumentException("Environment variable RPC_URL is not defined")),Environment.GetEnvironmentVariable("RPC_TOKEN"));

	public static void Connect(string? name,Uri uri)=>Connect(name,uri,Environment.GetEnvironmentVariable("RPC_TOKEN"));

	public static void Connect(string? name,Uri uri,string? token)
		=>Connect(name,uri,
		          token==null
			          ?null
			          :new NameValueCollection{
				          {"Cookie","RPC_TOKEN="+token},
			          });

	public static void Connect(string? name,Uri uri,NameValueCollection? headers)=>_=ClientConnectionWebSocket.Connect(name,uri,headers).Catch(Logger.Error);
}