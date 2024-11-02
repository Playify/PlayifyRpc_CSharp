using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Data.Objects;

/**
This has to be used instead of using Dictionary&lt;string,?&gt; directly,
as Dictionary would better assembles a JavaScript Map, instead of a JavaScript Object

This is similar to an ExpandoObject, but with a predefined value type
*/
[PublicAPI]
public sealed class StringMap<T>:InsertionOrderDictionary<string,T>,IRpcDataObject{
	public StringMap(){
	}

	public StringMap(IEqualityComparer<string> comparer):base(comparer){
	}

	public StringMap(IReadOnlyDictionary<string,T> dictionary){
		foreach(var kv in dictionary)
			Add(kv);
	}

	public StringMap(IReadOnlyDictionary<string,T> dictionary,IEqualityComparer<string> comparer):base(comparer){
		foreach(var kv in dictionary)
			Add(kv);
	}


	bool IRpcDataObject.TrySetProps(IEnumerable<(string key,RpcDataPrimitive value)> props,bool throwOnError,RpcDataPrimitive original)=>props.All(tuple=>{
		try{
			return tuple.value.TryTo(out T? child,throwOnError)&&this!.TryAdd(tuple.key,child);
		} catch(Exception e){
			throw new InvalidCastException("Error converting primitive "+original+" to "+RpcTypeStringifier.FromType(GetType())+
			                               ", due to property "+JsonString.Escape(tuple.key),e);
		}
	});

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(Dictionary<object,RpcDataPrimitive> already)
		=>this.Select(kv=>(kv.Key,RpcDataPrimitive.From(kv.Value,already)));
}

[PublicAPI]
public sealed class StringMap:InsertionOrderDictionary<string,RpcDataPrimitive>,IRpcDataObject{
	bool IRpcDataObject.TrySetProps(IEnumerable<(string key,RpcDataPrimitive value)> props,bool throwOnError,RpcDataPrimitive original)
		=>props.All(tuple=>this.TryAdd(tuple.key,tuple.value));

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(Dictionary<object,RpcDataPrimitive> already)=>this.ToTuples();
}