using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{
	public static RpcException ReadException(this DataInput input)=>RpcException.Read(input);
	public static object? ReadDynamic(this DataInput input)=>input.ReadDynamic(new List<object>());
	public static object? ReadDynamic(this DataInput input,List<object> already)=>DynamicData.Read(input,already);
}