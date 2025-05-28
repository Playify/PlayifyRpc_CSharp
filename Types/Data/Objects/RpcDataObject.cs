using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Utils;

namespace PlayifyRpc.Types.Data.Objects;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors|ImplicitUseTargetFlags.Members)]
public abstract partial class RpcDataObject:IRpcDataObject,ICloneable{

	bool IRpcDataObject.TrySetProps(IEnumerable<(string key,RpcDataPrimitive value)> props,bool throwOnError,RpcDataTransformerAttribute? transformer,RpcDataPrimitive original)
		=>Reflection.SetProps(this,props,throwOnError,transformer,original,TrySetExtraProp);

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer)
		=>Reflection.GetProps(this,already,transformer,GetExtraProps);


	protected virtual bool TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError,RpcDataTransformerAttribute? transformer)=>false;
	protected virtual IEnumerable<(string key,RpcDataPrimitive value)> GetExtraProps(RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer)=>[];

	// ReSharper disable once InvokeAsExtensionMethod
	object ICloneable.Clone()=>RpcHelpers.Clone(this);
}