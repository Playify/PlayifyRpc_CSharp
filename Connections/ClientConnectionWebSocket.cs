using System.Collections.Specialized;
using System.Net;
using PlayifyRpc.Internal;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Web;

namespace PlayifyRpc.Connections;

internal class ClientConnectionWebSocket:ClientConnection{
	private readonly WebSocket _webSocket;

	private ClientConnectionWebSocket(WebSocket webSocket)=>_webSocket=webSocket;

	protected internal override Task SendRaw(DataOutputBuff buff){
		var (b,len)=buff.GetBufferAndLength();
		return _webSocket.Send(b,0,len);
	}

	private async Task ReceiveLoop(){
		await foreach(var (s,b) in _webSocket)
			if(s!=null) Console.WriteLine($"{this}: {s}");
			else _=Receive(new DataInputBuff(b)).ConfigureAwait(false);
	}

	public override async ValueTask DisposeAsync(){
		await base.DisposeAsync();
		_webSocket.Close();
	}


	internal static async Task Connect(Uri uri,NameValueCollection? headers){
		StartConnect(false);
		while(true)
			try{
				var reportedName=Rpc.NameOrId;
				var reportedTypes=new HashSet<string>();
				lock(RegisteredTypes.Registered) reportedTypes.UnionWith(RegisteredTypes.Registered.Keys);

				var query=reportedTypes.Aggregate(
					"name="+WebUtility.UrlEncode(reportedName),
					(q,type)=>q+"&type="+WebUtility.UrlEncode(type));
				uri=new UriBuilder(uri){Query=query}.Uri;

				await using var connection=new ClientConnectionWebSocket(await WebSocket.CreateWebSocketTo(uri,headers));
				Console.WriteLine("Connected to RPC");
				var loop=connection.ReceiveLoop();//receive loop must start before, otherwise a deadlock would occur, because no answers can be received

				await DoConnect(connection,reportedName,reportedTypes);

				await loop;

				StartConnect(true);
			} catch(Exception e){
				FailConnect(e);

				await Task.Delay(1000);
				Console.WriteLine("Reconnecting to RPC...");
			}
		// ReSharper disable once FunctionNeverReturns
	}
}