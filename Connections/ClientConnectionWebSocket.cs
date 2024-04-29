using System.Collections.Specialized;
using System.Net;
using PlayifyRpc.Internal;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Web;

namespace PlayifyRpc.Connections;

internal class ClientConnectionWebSocket:ClientConnection{
	private readonly WebSocket _webSocket;

	private ClientConnectionWebSocket(WebSocket webSocket)=>_webSocket=webSocket;

	protected internal override async Task SendRaw(DataOutputBuff buff){
		try{
			var (b,len)=buff.GetBufferAndLength();
			await _webSocket.Send(b,0,len);
		} catch(ObjectDisposedException){
			await DisposeAsync().AsTask();//Should be already closed, but close again, just in case
		}
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
				var reportedName=Rpc.Name;
				var reportedTypes=new HashSet<string>();

				var query="id="+WebUtility.UrlEncode(Rpc.Id);
				reportedTypes.Add("$"+Rpc.Id);

				if(reportedName!=null)
					query+="&name="+WebUtility.UrlEncode(reportedName);

				lock(RegisteredTypes.Registered)
					foreach(var type in RegisteredTypes.Registered.Keys)
						if(reportedTypes.Add(type))
							query+="&type="+WebUtility.UrlEncode(type);


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
				Console.WriteLine("Reconnecting to RPC");
			}
		// ReSharper disable once FunctionNeverReturns
	}
}