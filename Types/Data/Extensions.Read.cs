using PlayifyRpc.Internal.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{
	public static RpcException ReadException(this DataInput input)=>RpcException.Read(input);
	public static object? ReadDynamic(this DataInputBuff input)=>input.ReadDynamic(new Dictionary<int,object>());
	public static object? ReadDynamic(this DataInputBuff input,Dictionary<int,object> already)=>DynamicData.Read(input,already);
}