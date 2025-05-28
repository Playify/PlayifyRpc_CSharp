using System.Collections;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;
#if !NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
#endif

namespace PlayifyRpc.Types.Invokers;

[PublicAPI]
public class DictionaryInvoker(Dictionary<string,Delegate> dictionary):Invoker,IDictionary<string,Delegate>{
	public readonly Dictionary<string,Delegate> Dictionary=dictionary;
	private readonly Dictionary<Delegate,RpcInvoker.MethodCandidate> _candidateCache=new();

	public DictionaryInvoker():this(new Dictionary<string,Delegate>()){
	}


	public IEnumerator<KeyValuePair<string,Delegate>> GetEnumerator()=>Dictionary.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator()=>Dictionary.GetEnumerator();

	protected override Task<RpcDataPrimitive> DynamicInvoke(string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx){
		if(!Dictionary.TryGetValue(method,out var @delegate))
			@delegate=Dictionary.FirstOrNull(p=>p.Key.Equals(method,StringComparison.OrdinalIgnoreCase))?.Value
			          ??throw new RpcMethodNotFoundException(type,method);
		if(!_candidateCache.TryGetValue(@delegate,out var candidate))
			_candidateCache[@delegate]=candidate=RpcInvoker.MethodCandidate.Create(@delegate.Method)
			                                     ??throw new RpcMethodNotFoundException(type,method,"Method cannot be used");
		return RpcInvoker.InvokeThrow(@delegate.Target,[candidate],args,msg=>new RpcMethodNotFoundException(type,method,msg),ctx);
	}

	protected override ValueTask<string[]> GetMethods()=>new(Dictionary.Keys.ToArray());

	protected override ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,ProgrammingLanguage lang)
		=>Dictionary.TryGetValue(method,out var d)
			  ?new ValueTask<(string[] parameters,string returns)[]>([..RpcTypeStringifier.MethodSignatures(d,lang)])
			  :new ValueTask<(string[] parameters,string returns)[]>(Task.FromException<(string[] parameters,string returns)[]>(new RpcMethodNotFoundException(type,method)));


	//Used for collection initializer
	public void Add(string key,Delegate value)=>Dictionary.Add(key,value);
	public void Add(string key,Action value)=>Dictionary.Add(key,value);
	public void Add(string key,Func<Task> value)=>Dictionary.Add(key,value);
	public void Add<TRet>(string key,Func<TRet> value)=>Dictionary.Add(key,value);
	public void Add<T1,TRet>(string key,Func<T1,TRet> value)=>Dictionary.Add(key,value);
	public void Add<T1,T2,TRet>(string key,Func<T1,T2,TRet> value)=>Dictionary.Add(key,value);
	public void Add<T1,T2,T3,TRet>(string key,Func<T1,T2,T3,TRet> value)=>Dictionary.Add(key,value);
	public void Add<T1,T2,T3,T4,TRet>(string key,Func<T1,T2,T3,T4,TRet> value)=>Dictionary.Add(key,value);
	public void Add<T1>(string key,Action<T1> value)=>Dictionary.Add(key,value);
	public void Add<T1,T2>(string key,Action<T1,T2> value)=>Dictionary.Add(key,value);
	public void Add<T1,T2,T3>(string key,Action<T1,T2,T3> value)=>Dictionary.Add(key,value);
	public void Add<T1,T2,T3,T4>(string key,Action<T1,T2,T3,T4> value)=>Dictionary.Add(key,value);


	public bool ContainsKey(string key)=>Dictionary.ContainsKey(key);
	public bool Remove(string key){
		if(!Dictionary.Remove(key,out var val)) return false;
		_candidateCache.Remove(val);
		return true;
	}
#if NETFRAMEWORK
	public bool TryGetValue(string key,out Delegate value)=>Dictionary.TryGetValue(key,out value);
#else
	public bool TryGetValue(string key,[MaybeNullWhen(false)]out Delegate value)=>Dictionary.TryGetValue(key,out value);
#endif
	public Delegate this[string key]{
		get=>Dictionary[key];
		set{
			if(Dictionary.TryGetValue(key,out var old)&&old!=value) _candidateCache.Remove(old);
			Dictionary[key]=value;
		}
	}
	public ICollection<string> Keys=>Dictionary.Keys;
	public ICollection<Delegate> Values=>Dictionary.Values;
	public void Add(KeyValuePair<string,Delegate> item)=>((IDictionary<string,Delegate>)Dictionary).Add(item);

	public void Clear(){
		Dictionary.Clear();
		_candidateCache.Clear();
	}

	public bool Contains(KeyValuePair<string,Delegate> item)=>((IDictionary<string,Delegate>)Dictionary).Contains(item);
	public void CopyTo(KeyValuePair<string,Delegate>[] array,int arrayIndex)=>((IDictionary<string,Delegate>)Dictionary).CopyTo(array,arrayIndex);

	public bool Remove(KeyValuePair<string,Delegate> item){
		if(!((IDictionary<string,Delegate>)Dictionary).Remove(item)) return false;
		_candidateCache.Remove(item.Value);
		return true;
	}

	public int Count=>Dictionary.Count;
	public bool IsReadOnly=>((IDictionary<string,Delegate>)Dictionary).IsReadOnly;
}