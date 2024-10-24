using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.HelperClasses;
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

	public StringMap(IReadOnlyDictionary<string,T> dictionary){
		foreach(var kv in dictionary)
			Add(kv);
	}

	public bool TrySetProps(IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError)=>props.All(tuple=>
		tuple.primitive.TryTo(out T child,throwOnError)&&this.TryAdd(tuple.s,child));

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(Dictionary<object,RpcDataPrimitive> already)
		=>this.Select(kv=>(kv.Key,RpcDataPrimitive.From(kv.Value,already)));
}

[PublicAPI]
public sealed class StringMap:InsertionOrderDictionary<string,RpcDataPrimitive>,IRpcDataObject{
	public bool TrySetProps(IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError)=>props.All(tuple=>
		this.TryAdd(tuple.s,tuple.primitive));

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(Dictionary<object,RpcDataPrimitive> already)=>this.ToTuples();
}