using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Data.Objects;

[PublicAPI]
public abstract class RpcDataObjectExtraProps:RpcDataObject{
	[RpcHidden]public readonly InsertionOrderDictionary<string,RpcDataPrimitive> ExtraProps=new();

	protected override bool TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)=>ExtraProps.TryAdd(s,primitive);
	protected override IEnumerable<(string key,RpcDataPrimitive value)> GetExtraProps(RpcDataPrimitive.Already already)=>ExtraProps.ToTuples();
}