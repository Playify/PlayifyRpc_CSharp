using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types.Data;

namespace PlayifyRpc.Internal.Invokers;

[PublicAPI]
public class DictionaryInvoker:Invoker{
	private readonly Dictionary<string,Delegate> _dict;
		
	public DictionaryInvoker(Dictionary<string,Delegate> dict)=>_dict=dict;

	protected internal override object? DynamicInvoke(string? type,string method,object?[] args){
		if(!_dict.TryGetValue(method,out var func)) throw new Exception($"Method \"{method}\" not found in \"{type}\"");

		return func.Method.Invoke(func.Target,
		                          BindingFlags.OptionalParamBinding|
		                          BindingFlags.FlattenHierarchy|
		                          BindingFlags.InvokeMethod,DynamicBinder.Instance,args,null);
	}
}