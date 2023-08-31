using System.Collections.Specialized;
using PlayifyRpc.Internal;
using PlayifyUtils.Streams;
using PlayifyUtils.Web;

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



	internal static async Task Connect(Uri uri,NameValueCollection? headers,string? name){
		await RegisteredTypes.SetName(name);
		StartConnect(false);
		while(true){
			try{
				await using var connection=new ClientConnectionWebSocket(await WebSocket.CreateWebSocketTo(uri,headers));
				Console.WriteLine("Connected");
				var loop=connection.ReceiveLoop();//receive loop must start before, otherwise a deadlock would occur, because no answers can be received

				await DoConnect(connection);

				await loop;

				StartConnect(true);
			} catch(Exception e){
				FailConnect(e);

				await Task.Delay(1000);
				Console.WriteLine("Reconnecting...");
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}
}