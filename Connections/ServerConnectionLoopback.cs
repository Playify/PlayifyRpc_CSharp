using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Invokers;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Connections;

internal class ServerConnectionLoopback:ServerConnection{
	private readonly ServerConnectionLoopbackClient _otherSide;

	private bool _disposed;

	internal ServerConnectionLoopback(ServerConnectionLoopbackClient otherSide)=>_otherSide=otherSide;

	protected internal override Task SendRaw(DataOutputBuff buff){
		Task.Run((Action)(()=>_otherSide.Receive(new DataInputBuff(buff)).Catch(Console.Error.WriteLine)));
		return Task.CompletedTask;
	}

	public new Task Receive(DataInputBuff data)=>base.Receive(data);

	public override async ValueTask DisposeAsync(){
		if(_disposed) return;
		_disposed=true;
		await base.DisposeAsync();
		await _otherSide.DisposeAsync();
	}
}

internal class ServerConnectionLoopbackClient:ClientConnection{
	private readonly ServerConnectionLoopback _otherSide;

	private bool _disposed;

	private ServerConnectionLoopbackClient()=>_otherSide=new ServerConnectionLoopback(this);

	internal static async Task Connect(){
		await RegisteredTypes.SetName("RPC_SERVER_LOOPBACK");
		await RegisteredTypes.Register("Rpc",new TypeInvoker(typeof(RpcServer)));
		StartConnect(false);
		while(true)
			try{
				await using var connection=new ServerConnectionLoopbackClient();
				await DoConnect(connection);

				await Task.Delay(Timeout.Infinite);


				StartConnect(true);
			} catch(Exception e){
				FailConnect(e);

				await Task.Delay(1000);
				Console.WriteLine("Reconnecting to RPC...");
			}
		// ReSharper disable once FunctionNeverReturns
	}

	protected internal override Task SendRaw(DataOutputBuff buff){
		Task.Run((Action)(()=>_otherSide.Receive(new DataInputBuff(buff)).Catch(Console.Error.WriteLine)));
		return Task.CompletedTask;
	}

	public new Task Receive(DataInputBuff data)=>base.Receive(data);

	public override async ValueTask DisposeAsync(){
		if(_disposed) return;
		_disposed=true;
		await base.DisposeAsync();
		await _otherSide.DisposeAsync();
	}
}