using System.Collections;
using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyUtility.HelperClasses;

namespace PlayifyRpc.Types.Data;

/**
This has to be used instead of using Dictionary<string,?> directly,
as Dictionary would better assembles a JavaScript Map, instead of a JavaScript Object
Otherwise, an ExpandoObject can be used as well
*/
[PublicAPI]
public class StringMap<T>:ObjectTemplate,IEnumerable<KeyValuePair<string,T>>{
	#region Enumerable
	public readonly IDictionary<string,T> Dictionary;

	public StringMap()=>Dictionary=new InsertionOrderDictionary<string,T>();
	public StringMap(IDictionary<string,T> dict)=>Dictionary=dict;

	public void Add(string key,T value)=>Dictionary.Add(key,value);

	public IEnumerator<KeyValuePair<string,T>> GetEnumerator()=>Dictionary.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
	#endregion

	#region ObjectTemplate
	private protected override bool TrySetProperty(string key,object? value){
		if(!StaticallyTypedUtils.TryCast<T>(value,out var t)) return false;
		Dictionary[key]=t;
		return true;
	}

	protected internal override IEnumerable<(string key,object? value)> GetProperties()=>Dictionary.Select(pair=>(pair.Key,(object?)pair.Value));
	#endregion


	public static implicit operator Dictionary<string,T>(StringMap<T> sm)=>sm.Dictionary as Dictionary<string,T>??new Dictionary<string,T>(sm.Dictionary);
	public static implicit operator InsertionOrderDictionary<string,T>(StringMap<T> sm)=>sm.Dictionary as InsertionOrderDictionary<string,T>??new InsertionOrderDictionary<string,T>(sm.Dictionary);
	public static implicit operator StringMap<T>(Dictionary<string,T> dict)=>new(dict);
}