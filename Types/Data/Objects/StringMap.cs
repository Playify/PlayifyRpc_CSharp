using System.Collections;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.HelperClasses;

namespace PlayifyRpc.Types.Data.Objects;

/**
This has to be used instead of using Dictionary&lt;string,?&gt; directly,
as Dictionary would better assembles a JavaScript Map, instead of a JavaScript Object
Otherwise, an ExpandoObject can be used as well
*/
[PublicAPI]
public class StringMap<T>:ObjectTemplateBase,IEnumerable<KeyValuePair<string,T>>{
	#region Enumerable
	public readonly IDictionary<string,T> Dictionary;

	public StringMap()=>Dictionary=new InsertionOrderDictionary<string,T>();
	public StringMap(IDictionary<string,T> dict)=>Dictionary=dict;

	public void Add(string key,T value)=>Dictionary.Add(key,value);

	// ReSharper disable once NotDisposedResourceIsReturned
	public IEnumerator<KeyValuePair<string,T>> GetEnumerator()=>Dictionary.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();

	public T this[string key]{
		get=>Dictionary.TryGetValue(key,out var value)?value:throw new KeyNotFoundException(key);
		set=>TrySetProperty(key,value,true);
	}
	#endregion

	#region ObjectTemplate
	public override bool TrySetProperty(string key,object? value,bool throwOnError){
		if(!DynamicCaster.TryCast(value,out T t,throwOnError)) return false;
		Dictionary[key]=t;
		return true;
	}

	public override bool TryGetProperty(string key,out object? value){
		var b=Dictionary.TryGetValue(key,out var tValue);
		value=b?tValue:default;
		return b;
	}

	public override IEnumerable<(string key,object? value)> GetProperties()=>Dictionary.Select(pair=>(pair.Key,(object?)pair.Value));
	#endregion


	public static implicit operator Dictionary<string,T>(StringMap<T> sm)=>sm.Dictionary as Dictionary<string,T>??new Dictionary<string,T>(sm.Dictionary);
	public static implicit operator InsertionOrderDictionary<string,T>(StringMap<T> sm)=>sm.Dictionary as InsertionOrderDictionary<string,T>??new InsertionOrderDictionary<string,T>(sm.Dictionary);
	public static implicit operator StringMap<T>(Dictionary<string,T> dict)=>new(dict);
	public static implicit operator StringMap<T>(InsertionOrderDictionary<string,T> dict)=>new(dict);
}