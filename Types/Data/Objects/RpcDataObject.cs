using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Utils;

namespace PlayifyRpc.Types.Data.Objects;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors|ImplicitUseTargetFlags.Members)]
public abstract partial class RpcDataObject:IRpcDataObject,ICloneable{

	bool IRpcDataObject.TrySetProps(IEnumerable<(string key,RpcDataPrimitive value)> props,bool throwOnError,RpcDataPrimitive original)
		=>Reflection.SetProps(this,props,throwOnError,original);

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetProps(RpcDataPrimitive.Already already)
		=>Reflection.GetProps(this,already,GetExtraProps);


	protected virtual bool TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)=>false;
	protected virtual IEnumerable<(string key,RpcDataPrimitive value)> GetExtraProps(RpcDataPrimitive.Already already)=>[];

	// ReSharper disable once InvokeAsExtensionMethod
	object ICloneable.Clone()=>RpcHelpers.Clone(this);
}