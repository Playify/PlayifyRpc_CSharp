using System.Collections.Specialized;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Web;

namespace PlayifyRpc.Connections;

public class ServerConnectionWebSocket:ServerConnection{
	internal readonly Task<WebSocket> WebSocketTask;

	public ServerConnectionWebSocket(Func<Task<WebSocket>> webSocket,NameValueCollection query)
		:base(
			query.GetValues("id") is not{} id?null:id.SingleOrDefault()??throw new ArgumentException("Multiple 'id' parameters")
		){/*
   :base((query.GetValues("id")??throw new ArgumentException("Missing 'id' parameter"))
		    .SingleOrDefault()??throw new ArgumentException("Multiple 'id' parameters")){*/

		if(query.GetValues("name") is{} names)
			Name=names.SingleOrDefault()??throw new ArgumentException("Multiple 'name' parameters");
		Register(query.GetValues("type")??Array.Empty<string>(),false);

		WebSocketTask=webSocket();
	}

	protected internal override async Task SendRaw(DataOutputBuff buff){
		var (b,len)=buff.GetBufferAndLength();
		await (await WebSocketTask).Send(b,0,len);
	}

	public async Task ReceiveLoop(){
		await foreach(var (s,b) in await WebSocketTask)
			if(s!=null) Console.WriteLine($"{this}: {s}");
			else _=Receive(new DataInputBuff(b)).ConfigureAwait(false);
	}
}