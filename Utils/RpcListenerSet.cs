using System.Collections;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.HelperClasses.Dispose;

namespace PlayifyRpc.Utils;

[PublicAPI]
public class RpcListenerSet:IEnumerable<FunctionCallContext>{
	private readonly HashSet<FunctionCallContext> _set=[];
	private List<Action>? _dispose;

	private RpcDataPrimitive.Already Already=>new(a=>{
		lock(this) (_dispose??=[]).Add(a);
	});

	public RpcDataPrimitive[]? LastMessage{get;private set;}


	public void SendAllRaw(params RpcDataPrimitive[] args){
		LastMessage=args;
		lock(_set)
			foreach(var ctx in _set)
				ctx.SendMessageRaw(args);
	}

	public void SendAll(params object?[] args)=>SendAllRaw(RpcDataPrimitive.FromArray(args,Already));

	public void SendLazySingle(Func<object?> generate)=>SendLazyRaw(()=>[RpcDataPrimitive.From(generate(),Already)]);

	public void SendLazy(Func<object?[]> generate)=>SendLazyRaw(()=>RpcDataPrimitive.FromArray(generate(),Already));

	public void SendLazyRaw(Func<RpcDataPrimitive> generate)=>SendLazyRaw(()=>[generate()]);

	public void SendLazyRaw(Func<RpcDataPrimitive[]> generate){
		lock(_set)
			if(_set.Count==0)
				return;
		var args=generate();
		SendAllRaw(args);
	}


	public IDisposable Add(FunctionCallContext ctx){
		lock(_set) _set.Add(ctx);
		return new CallbackAsDisposable(()=>Remove(ctx));
	}

	public void Remove(FunctionCallContext ctx){
		lock(_set) _set.Remove(ctx);
		lock(this){
			if(_set.Count!=0||_dispose==null) return;
			foreach(var action in _dispose) action();
			_dispose=null;
		}
	}

	public bool Contains(FunctionCallContext ctx){
		lock(_set) return _set.Contains(ctx);
	}

	public void Clear(){
		lock(_set) _set.Clear();
		lock(this){
			if(_dispose==null) return;
			foreach(var action in _dispose) action();
			_dispose=null;
		}
	}

	public int Count{
		get{
			lock(_set) return _set.Count;
		}
	}

	public static explicit operator HashSet<FunctionCallContext>(RpcListenerSet listeners)=>listeners._set;


	[MustDisposeResource]
	IEnumerator<FunctionCallContext> IEnumerable<FunctionCallContext>.GetEnumerator(){
		lock(_set) return _set.ToList().GetEnumerator();
	}

	[MustDisposeResource]
	public IEnumerator GetEnumerator()=>((IEnumerable<FunctionCallContext>)this).GetEnumerator();
}