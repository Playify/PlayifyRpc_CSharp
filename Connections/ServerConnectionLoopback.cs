using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils;

namespace PlayifyRpc.Connections;

internal class ServerConnectionLoopback:ServerConnection{
	private readonly ServerConnectionLoopbackClient _otherSide;
	internal ServerConnectionLoopback(ServerConnectionLoopbackClient otherSide){
		_otherSide=otherSide;
	}

	protected internal override Task SendRaw(DataOutputBuff buff){
		Task.Run((Action)(()=>_otherSide.Receive(new DataInputBuff(buff)).Catch(Console.Error.WriteLine)));
		return Task.CompletedTask;
	}

	public new Task Receive(DataInputBuff data)=>base.Receive(data);

	private bool _disposed;
	public override async ValueTask DisposeAsync(){
		if(_disposed) return;
		_disposed=true;
		await base.DisposeAsync();
		await _otherSide.DisposeAsync();
	}
}

internal class ServerConnectionLoopbackClient:ClientConnection{
	private readonly ServerConnectionLoopback _otherSide;

	internal static async Task Connect(){
		await RegisteredTypes.SetName("RPC_SERVER_LOOPBACK");
		await RegisteredTypes.Register("Rpc",new TypeInvoker(typeof(RpcServer)));
		StartConnect(false);
		while(true){
			try{
				await using var connection=new ServerConnectionLoopbackClient();
				await DoConnect(connection);
				
				await Task.Delay(Timeout.Infinite);
				
				
				StartConnect(true);
			} catch(Exception e){
				FailConnect(e);

				await Task.Delay(1000);
				Console.WriteLine("Reconnecting...");
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}

	private ServerConnectionLoopbackClient(){
		_otherSide=new ServerConnectionLoopback(this);
	}

	protected internal override Task SendRaw(DataOutputBuff buff){
		Task.Run((Action)(()=>_otherSide.Receive(new DataInputBuff(buff)).Catch(Console.Error.WriteLine)));
		return Task.CompletedTask;
	}

	public new Task Receive(DataInputBuff data)=>base.Receive(data);

	private bool _disposed;
	public override async ValueTask DisposeAsync(){
		if(_disposed) return;
		_disposed=true;
		await base.DisposeAsync();
		await _otherSide.DisposeAsync();
	}
}