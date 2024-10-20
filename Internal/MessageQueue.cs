using System.Threading.Tasks.Dataflow;
using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Internal;

internal class MessageQueue:IAsyncEnumerable<RpcDataPrimitive[]>{
	private readonly Task _task;
	private readonly HashSet<Action<RpcDataPrimitive[]>> _receivers=[];
	private List<RpcDataPrimitive[]>? _initialPending=[];

	public MessageQueue(Task task){
		_task=task;
	}

	public void AddMessageListener(Delegate a)=>AddMessageListenerRaw(msg=>DynamicBinder.InvokeThrow(a,msg));

	public void AddMessageListenerRaw(Action<RpcDataPrimitive[]> a){
		lock(_receivers)
			if(_initialPending!=null){
				_receivers.Add(a);
				foreach(var objects in _initialPending)
					try{
						a(objects);
					} catch(Exception e){
						Rpc.Logger.Warning("Error while handling pending message: "+e);
					}
				_initialPending=null;
			} else _receivers.Add(a);
	}

	public async IAsyncEnumerator<RpcDataPrimitive[]> GetAsyncEnumerator(CancellationToken cancelToken=new()){
		var receive=new BufferBlock<RpcDataPrimitive[]>();

		AddMessageListenerRaw(msg=>receive.Post(msg));
		// ReSharper disable once MethodSupportsCancellation
		_=_task.ContinueWith(_=>receive.Complete());


		while(await receive.OutputAvailableAsync(cancelToken).ConfigureAwait(false))
		while(receive.TryReceive(out var item))
			yield return item;
		await receive.Completion.ConfigureAwait(false);// Propagate possible exception
	}


	internal virtual void DoReceiveMessage(RpcDataPrimitive[] args){
		if(_task.IsCompleted) return;

		Action<RpcDataPrimitive[]>[] list;
		lock(_receivers){
			list=_receivers.ToArray();
			if(_initialPending!=null){
				_initialPending.Add(args);
				return;
			}
		}
		foreach(var func in list)
			try{
				func(args);
			} catch(Exception e){
				Rpc.Logger.Warning("Error while receiving message: "+e);
			}
	}
}