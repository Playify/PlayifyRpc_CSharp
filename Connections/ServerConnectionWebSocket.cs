using System.Collections.Specialized;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;
using PlayifyUtility.Web;

namespace PlayifyRpc.Connections;

internal class ServerConnectionWebSocket:ServerConnection{
	internal readonly Task<WebSocket> WebSocketTask;

	public ServerConnectionWebSocket(Func<Task<WebSocket>> webSocket,NameValueCollection query)
		:base(
			query.GetValues("id") is not{} ids?null:ids.SingleOrDefault()??throw new ArgumentException("Multiple 'id' parameters")
		){
		try{
			if(query.GetValues("name") is{} names)
				Name=names.SingleOrDefault()??throw new ArgumentException("Multiple 'name' parameters");
			Register(query.GetValues("type")??[],false);

			WebSocketTask=webSocket();
		} catch(Exception e){
			ForceUnregister();
			Logger.Warning($"Connection rejected ({e.Message})");
			throw;
		}
	}

	protected internal override async Task SendRaw(DataOutputBuff buff){
		try{
			var (b,len)=buff.GetBufferAndLength();
			await (await WebSocketTask).Send(b,0,len);
		} catch(ObjectDisposedException){
			await DisposeAsync().AsTask();//Should be already closed, but close again, just in case
		}
	}

	public async Task ReceiveLoop(){
		await foreach(var (s,b) in await WebSocketTask)
			if(s!=null) Logger.Log("WebSocket Message: "+s);
			else Receive(new DataInputBuff(b)).Background(e=>Logger.Warning("Error receiving Packet: "+e));
	}
}