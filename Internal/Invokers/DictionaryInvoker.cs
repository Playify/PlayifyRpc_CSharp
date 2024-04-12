using System.Collections;
using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class DictionaryInvoker:Invoker,IEnumerable<KeyValuePair<string,Delegate>>{
	public readonly Dictionary<string,Delegate> Dictionary;

	public DictionaryInvoker(Dictionary<string,Delegate> dictionary)=>Dictionary=dictionary;
	public DictionaryInvoker()=>Dictionary=new Dictionary<string,Delegate>();


	public IEnumerator<KeyValuePair<string,Delegate>> GetEnumerator()=>Dictionary.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator()=>Dictionary.GetEnumerator();

	protected override object? DynamicInvoke(string? type,string method,object?[] args){
		if(!Dictionary.TryGetValue(method,out var func)) throw new RpcMethodNotFoundException(type,method);
		try{
			return func.Method.Invoke(func.Target,
			                          BindingFlags.OptionalParamBinding|
			                          BindingFlags.FlattenHierarchy|
			                          BindingFlags.InvokeMethod,
			                          DynamicBinder.Instance,
			                          args,
			                          null!);
		} catch(TargetInvocationException e){
			throw RpcException.Convert(e.InnerException??e,true);
		}
	}

	protected override ValueTask<string[]> GetMethods()=>new(Dictionary.Keys.ToArray());

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
	public bool Remove(string key)=>Dictionary.Remove(key);
}