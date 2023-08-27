using System.Threading.Tasks.Dataflow;
using PlayifyRpc.Types.Data;
using PlayifyUtils.Utils;

namespace PlayifyRpc.Types.Functions;

public abstract class SendReceive:IAsyncEnumerable<object?[]>{
	private List<object?[]>? _initialPending=new();
	private readonly HashSet<MessageFunc> _receivers=new();

	public abstract bool Finished{get;}
	public abstract Task<object?> Task{get;}

	public abstract void SendMessage(params object?[] args);
	public void AddMessageListener<T1>(Action<T1> a)=>AddMessageListener(args=>a(DataTemplate.DoCast<T1>(args[0])));
	public void AddMessageListener<T1,T2>(Action<T1,T2> a)=>AddMessageListener(args=>a(DataTemplate.DoCast<T1>(args[0]),DataTemplate.DoCast<T2>(args[1])));
	public void AddMessageListener<T1,T2,T3>(Action<T1,T2,T3> a)=>AddMessageListener(args=>a(DataTemplate.DoCast<T1>(args[0]),DataTemplate.DoCast<T2>(args[1]),DataTemplate.DoCast<T3>(args[2])));
	public void AddMessageListener<T1,T2,T3,T4>(Action<T1,T2,T3,T4> a)=>AddMessageListener(args=>a(DataTemplate.DoCast<T1>(args[0]),DataTemplate.DoCast<T2>(args[1]),DataTemplate.DoCast<T3>(args[2]),DataTemplate.DoCast<T4>(args[3])));

	public virtual void AddMessageListener(MessageFunc a){
		lock(_receivers){
			if(_initialPending!=null){
				_receivers.Add(a);
				foreach(var objects in _initialPending){
					try{
						a(objects);
					} catch(Exception e){
						Console.WriteLine("Error receiving pending: "+e);
					}
				}
				_initialPending=null;
			} else _receivers.Add(a);
		}
	}


	internal virtual void DoReceiveMessage(object?[] args){
		if(Finished)return;
		
		MessageFunc[] list;
		lock(_receivers){
			list=_receivers.ToArray();
			if(_initialPending!=null){
				_initialPending.Add(args);
				return;
			}
		}
		foreach(var func in list){
			try{
				func(args);
			} catch(Exception e){
				Console.WriteLine("Error receiving: "+e);
			}
		}
	}
	public async IAsyncEnumerator<object?[]> GetAsyncEnumerator(CancellationToken cancelToken=new()){
		var receive=new BufferBlock<object?[]>();
		
		AddMessageListener(msg=>receive.Post(msg));
		_=Task.Finally(()=>receive.Complete());
		

		while(await receive.OutputAvailableAsync(cancelToken).ConfigureAwait(false))
		while(receive.TryReceive(out var item))
			yield return item;
		await receive.Completion.ConfigureAwait(false);// Propagate possible exception
	}
}