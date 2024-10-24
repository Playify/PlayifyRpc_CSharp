using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data.Objects;

public partial class RpcDataObject:IRpcDataObject{

	bool IRpcDataObject.TrySetProps(IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError){
		var (_,setters,settersIgnoreCase)=GetTypeInfos(GetType());

		return props.All(tuple=>{
			var (key,primitive)=tuple;
			if(setters.TryGetValue(key,out var setter)
			   ||settersIgnoreCase.TryGetValue(key,out setter)){
				if(!primitive.TryTo(setter.type,out var result,throwOnError)) return false;
				setter.setValue(this,result);
				return true;
			}
			return TrySetExtraProp(key,primitive,throwOnError);
		});
	}

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(Dictionary<object,RpcDataPrimitive> already){
		foreach(var (key,getValue) in GetTypeInfos(GetType()).getters)
			yield return (key,RpcDataPrimitive.From(getValue(this)));
		foreach(var tuple in GetExtraProps(already))
			yield return tuple;
	}

	protected virtual bool TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)=>false;
	protected virtual IEnumerable<(string key,RpcDataPrimitive value)> GetExtraProps(Dictionary<object,RpcDataPrimitive> already)=>[];

}