using System.Collections.Specialized;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Web;

namespace PlayifyRpc.Connections;

public class ServerConnectionWebSocket:ServerConnection{
	private readonly Task<WebSocket> _webSocket;

	public ServerConnectionWebSocket(Task<WebSocket> webSocket,NameValueCollection query){
		_webSocket=webSocket;
		foreach(var name in query.GetValues("name")??Array.Empty<string>())
			SetName(name);
		Register(query.GetValues("type")??Array.Empty<string>(),false);
	}

	protected internal override async Task SendRaw(DataOutputBuff buff){
		var (b,len)=buff.GetBufferAndLength();
		await (await _webSocket).Send(b,0,len);
	}

	public async Task ReceiveLoop(){
		await foreach(var (s,b) in await _webSocket)
			if(s!=null) Console.WriteLine($"{this}: {s}");
			else _=Receive(new DataInputBuff(b)).ConfigureAwait(false);
	}
}