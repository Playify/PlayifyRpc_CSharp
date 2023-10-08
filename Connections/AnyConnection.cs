using PlayifyRpc.Types.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Connections;

public abstract class AnyConnection{
	protected internal abstract Task SendRaw(DataOutputBuff data);
	protected abstract Task Receive(DataInputBuff data);
	
	protected abstract void RespondedToCallId(int callId);


	protected async Task Resolve(int callId,object? result){
		try{
			var buff=new DataOutputBuff();
			buff.WriteByte((byte)PacketType.FunctionSuccess);
			buff.WriteLength(callId);
			buff.WriteDynamic(result);
			await SendRaw(buff);
		} finally{
			RespondedToCallId(callId);
		}
	}

	internal async Task ResolveRaw(int callId,DataInputBuff data){
		try{
			var buff=new DataOutputBuff();
			buff.WriteByte((byte)PacketType.FunctionSuccess);
			buff.WriteLength(callId);

			buff.Write(data);
			await SendRaw(buff);
		} finally{
			RespondedToCallId(callId);
		}
	}

	protected async Task Reject(int callId,Exception error){
		try{
			var buff=new DataOutputBuff();
			buff.WriteByte((byte)PacketType.FunctionError);
			buff.WriteLength(callId);
			buff.WriteException(error);
			await SendRaw(buff);
		} finally{
			RespondedToCallId(callId);
		}
	}

	internal async Task RejectRaw(int callId,DataInputBuff data){
		try{
			var buff=new DataOutputBuff();
			buff.WriteByte((byte)PacketType.FunctionError);
			buff.WriteLength(callId);

			buff.Write(data);
			await SendRaw(buff);
		} finally{
			RespondedToCallId(callId);
		}
	}

	internal async Task CancelRaw(int respondId,DataInputBuff? data){
		var buff=new DataOutputBuff();
		buff.WriteByte((byte)PacketType.FunctionCancel);
		buff.WriteLength(respondId);
		if(data!=null) buff.Write(data);//would be empty, but if future versions send data, then let them
		await SendRaw(buff);
	}
}