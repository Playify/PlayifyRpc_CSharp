using System.Collections;
using JetBrains.Annotations;
using PlayifyRpc.Internal;

namespace PlayifyRpc.Types.Data;

//This has to be used instead of using Dictionary directly, as Dictionary better assembles a JavaScript Map, instead of a JavaScript Object
[PublicAPI]
public class StringMap<T>:ObjectTemplate,IEnumerable<KeyValuePair<string,T>>{
	#region Enumerable
	public readonly Dictionary<string,T> Dictionary;

	private StringMap()=>Dictionary=new Dictionary<string,T>();
	private StringMap(Dictionary<string,T> dict)=>Dictionary=dict;

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


	public static implicit operator Dictionary<string,T>(StringMap<T> sm)=>sm.Dictionary;
	public static implicit operator StringMap<T>(Dictionary<string,T> dict)=>new(dict);
}