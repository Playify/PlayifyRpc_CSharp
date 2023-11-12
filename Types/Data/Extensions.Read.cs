using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{

	public static RpcException ReadException(this DataInput input){
		var type=input.ReadString();
		return new RpcException(type,input.ReadString()??"???",input.ReadString(),input.ReadString());
	}

	public static object? ReadDynamic(this DataInput input)=>input.ReadDynamic(new List<object>());
	public static object? ReadDynamic(this DataInput input,List<object> already)=>DynamicData.Read(input,already);
}