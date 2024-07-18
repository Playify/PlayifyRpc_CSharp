using PlayifyRpc.Internal.Data;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Data.Objects;

public abstract class ObjectTemplateLenient:ObjectTemplateBase{
	private readonly InsertionOrderDictionary<string,object?> _extraProps=new(StringComparer.OrdinalIgnoreCase);
	public InsertionOrderDictionary<string,object?> GetExtraProps()=>_extraProps;

	public override bool TrySetProperty(string key,object? value,bool throwOnError){
		if(TrySetReflectionProperty(key,value,throwOnError).TryGet(out var b)) return b;
		if(DynamicCaster.TryCast(value,out object? newValue,false)) value=newValue;
		if(throwOnError) _extraProps.Add(key,value);
		else _extraProps[key]=value;
		return true;
	}

	public override bool TryGetProperty(string key,out object? value)=>TryGetReflectionProperty(key,out value)||_extraProps.TryGetValue(key,out value);

	public override IEnumerable<(string key,object? value)> GetProperties()=>GetReflectionProperties().Concat(_extraProps.ToTuples());
}

public abstract class ObjectTemplateLenient<T>:ObjectTemplateBase{
	private readonly InsertionOrderDictionary<string,T> _extraProps=new(StringComparer.OrdinalIgnoreCase);
	public InsertionOrderDictionary<string,T> GetExtraProps()=>_extraProps;

	public override bool TrySetProperty(string key,object? value,bool throwOnError){
		if(TrySetReflectionProperty(key,value,throwOnError).TryGet(out var b)) return b;
		if(!DynamicCaster.TryCast(value,out T newValue,throwOnError)) return false;
		if(throwOnError) _extraProps.Add(key,newValue);
		else _extraProps[key]=newValue;
		return true;
	}

	public override bool TryGetProperty(string key,out object? value){
		if(TryGetReflectionProperty(key,out value)) return true;
		if(!_extraProps.TryGetValue(key,out var tValue)) return false;
		value=tValue;
		return true;
	}

	public override IEnumerable<(string key,object? value)> GetProperties()=>GetReflectionProperties().Concat(_extraProps.Select(kv=>(kv.Key,(object?)kv.Value)));
}