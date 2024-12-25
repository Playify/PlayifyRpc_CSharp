using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data.Objects;

public interface IRpcDataObject{
	bool TrySetProps(IEnumerable<(string key,RpcDataPrimitive value)> props,bool throwOnError,RpcDataPrimitive original);
	IEnumerable<(string key,RpcDataPrimitive value)> GetProps(RpcDataPrimitive.Already already);
}