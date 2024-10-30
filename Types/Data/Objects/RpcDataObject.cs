using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data.Objects;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors|ImplicitUseTargetFlags.WithMembers)]
[MeansImplicitUse(ImplicitUseTargetFlags.Members)]
public abstract partial class RpcDataObject:IRpcDataObject{

	bool IRpcDataObject.TrySetProps(IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError){
		var (_,setters,settersIgnoreCase)=GetTypeInfos(GetType());

		foreach(var (key,primitive) in props)
			if(setters.TryGetValue(key,out var setter)
			   ||settersIgnoreCase.TryGetValue(key,out setter)){
				if(!primitive.TryTo(setter.type,out var result,throwOnError)) return false;
				setter.setValue(this,result);
			} else if(!TrySetExtraProp(key,primitive,throwOnError)) return false;
		return true;
	}

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(Dictionary<object,RpcDataPrimitive> already){
		foreach(var (key,getValue) in GetTypeInfos(GetType()).getters)
			yield return (key,RpcDataPrimitive.From(getValue(this),already));
		foreach(var tuple in GetExtraProps(already))
			yield return tuple;
	}

	protected virtual bool TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)=>false;
	protected virtual IEnumerable<(string key,RpcDataPrimitive value)> GetExtraProps(Dictionary<object,RpcDataPrimitive> already)=>[];


	public static bool DefaultTrySetProps<T>(ref T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,
		Func<string,RpcDataPrimitive,bool,bool>? extraProp=null) where T : notnull{
		var (_,setters,settersIgnoreCase)=GetTypeInfos(typeof(T));

		foreach(var (key,primitive) in props)
			if(setters.TryGetValue(key,out var setter)
			   ||settersIgnoreCase.TryGetValue(key,out setter)){
				if(!primitive.TryTo(setter.type,out var result,throwOnError)) return false;
				object boxed=thiz;
				setter.setValue(boxed,result);
				thiz=(T)boxed;
			} else if(extraProp==null||!extraProp(key,primitive,throwOnError)) return false;
		return true;
	}

	public static IEnumerable<(string key,RpcDataPrimitive value)> DefaultGetProps<T>(T thiz,Dictionary<object,RpcDataPrimitive> already,
		Func<Dictionary<object,RpcDataPrimitive>,IEnumerable<(string key,RpcDataPrimitive value)>>? extraProps=null) where T : notnull{
		foreach(var (key,getValue) in GetTypeInfos(typeof(T)).getters)
			yield return (key,RpcDataPrimitive.From(getValue(thiz),already));
		if(extraProps==null)
			yield break;
		foreach(var tuple in extraProps(already))
			yield return tuple;
	}
}