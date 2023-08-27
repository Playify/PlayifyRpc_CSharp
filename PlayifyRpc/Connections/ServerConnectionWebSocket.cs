using PlayifyUtils.Streams;
using PlayifyUtils.Web;

namespace PlayifyRpc.Connections;

public class ServerConnectionWebSocket:ServerConnection{
	private readonly WebSocket _webSocket;

	internal ServerConnectionWebSocket(WebSocket webSocket)=>_webSocket=webSocket;

	protected internal override Task SendRaw(DataOutputBuff buff){
		var (b,len)=buff.GetBufferAndLength();
		return _webSocket.Send(b,0,len);
	}

	public async Task ReceiveLoop(){
		await foreach(var (s,b) in _webSocket)
			if(s!=null) Console.WriteLine($"{this}: {s}");
			else _=Receive(new DataInputBuff(b)).ConfigureAwait(false);
	}
}