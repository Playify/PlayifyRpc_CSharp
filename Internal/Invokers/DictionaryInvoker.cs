using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types.Data;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class DictionaryInvoker:Invoker{
	public readonly Dictionary<string,Delegate> Dictionary;
		
	public DictionaryInvoker(Dictionary<string,Delegate> dictionary)=>Dictionary=dictionary;
	public DictionaryInvoker()=>Dictionary=new Dictionary<string,Delegate>();

	protected internal override object? DynamicInvoke(string? type,string method,object?[] args){
		if(!Dictionary.TryGetValue(method,out var func)) throw new Exception($"Method \"{method}\" not found in \"{type}\"");

		return func.Method.Invoke(func.Target,
		                          BindingFlags.OptionalParamBinding|
		                          BindingFlags.FlattenHierarchy|
		                          BindingFlags.InvokeMethod,DynamicBinder.Instance,args,null);
	}

	public void Add(string key,Delegate value)=>Dictionary.Add(key,value);//Used for collection initializer
	public bool ContainsKey(string key)=>Dictionary.ContainsKey(key);
	public bool Remove(string key)=>Dictionary.Remove(key);
}