using PlayifyRpc.Internal.Data;

namespace PlayifyRpc.Types.Data.Objects;

public interface IRpcDataObject{
	bool TrySetProps(IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError);
	IEnumerable<(string key,RpcDataPrimitive value)> GetProps(Dictionary<object,RpcDataPrimitive> already);
}