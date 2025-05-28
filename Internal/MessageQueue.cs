using System.Threading.Tasks.Dataflow;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

internal class MessageQueue(Task task,string? type,string? method,RpcDataPrimitive[]? args,Func<Task<string>> getCaller):IAsyncEnumerable<RpcDataPrimitive[]>{
	private readonly HashSet<Action<RpcDataPrimitive[]>> _receivers=[];
	private List<RpcDataPrimitive[]>? _initialPending=[];

	public void AddMessageListener(Delegate a){
		var candidate=RpcInvoker.MethodCandidate.Create(a.Method)??throw new MissingMethodException("ref and out parameters are not allowed");
		candidate.ReturnTransformer??=RpcDataTransformerAttribute.RpcDataNullTransformer.Instance;

		AddMessageListenerRaw(msg=>
			RpcInvoker.InvokeThrow(a.Target,[candidate],msg,
				          s=>new RpcException(null,null,s,""),null)
			          .Catch(e=>{
				          Rpc.Logger.Warning(new RpcException(null,null,"Error while handling pending message","",e)
				                             .Append(type,method,args)
				                             .ToString());
			          }));
	}

	public void AddMessageListenerRaw(Action<RpcDataPrimitive[]> a){
		lock(_receivers)
			if(_initialPending!=null){
				_receivers.Add(a);
				foreach(var objects in _initialPending)
					try{
						a(objects);
					} catch(Exception e){
						Rpc.Logger.Warning(new RpcException(null,null,"Error while handling pending message","",e)
						                   .Append(type,method,args)
						                   .ToString());
					}
				_initialPending=null;
			} else _receivers.Add(a);
	}

	public async IAsyncEnumerator<RpcDataPrimitive[]> GetAsyncEnumerator(CancellationToken cancelToken=new()){
		var receive=new BufferBlock<RpcDataPrimitive[]>();

		AddMessageListenerRaw(msg=>receive.Post(msg));
		_=task.ContinueWith(_=>receive.Complete(),CancellationToken.None);


		while(await receive.OutputAvailableAsync(cancelToken).ConfigureAwait(false))
		while(receive.TryReceive(out var item))
			yield return item;
		await receive.Completion.ConfigureAwait(false);// Propagate possible exception
	}


	internal async void DoReceiveMessage(RpcDataPrimitive[] msg){
		try{
			if(task.IsCompleted) return;

			Action<RpcDataPrimitive[]>[] list;
			lock(_receivers){
				list=_receivers.ToArray();
				if(_initialPending!=null){
					_initialPending.Add(msg);
					return;
				}
			}

			List<Exception>? exceptions=null;
			foreach(var func in list)
				try{
					func(msg);
				} catch(Exception e){
					(exceptions??=[]).Add(e);
				}
			if(exceptions==null) return;

			string caller;
			try{
				caller=await getCaller();
			} catch(Exception){
				caller="<<Unknown>>";
			}

			Rpc.Logger.Warning(new RpcException(null,null,"Error while receiving message from "+caller,"",
				                   exceptions.Count==1?exceptions[0]:new AggregateException(exceptions))
			                   .Append(type,method,args)
			                   .ToString());
		} catch(Exception e){
			Rpc.Logger.Critical("Error receiving message: "+e);
		}
	}
}