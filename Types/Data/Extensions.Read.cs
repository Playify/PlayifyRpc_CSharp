using PlayifyUtils.Streams;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{
	public static byte[] ReadFully(this DataInput input,int length){//TODO move to PlayifyUtility
		var bytes=new byte[length];
		input.ReadFully(bytes);
		return bytes;
	}

	public static RpcException ReadException(this DataInput input){
		var type=input.ReadString();
		if(type=="UnknownType"){

		}
		return new RpcException(type,input.ReadString()??"???",input.ReadString(),input.ReadString());
	}

	public static object? ReadDynamic(this DataInput input)=>input.ReadDynamic(new List<object>());
	public static object? ReadDynamic(this DataInput input,List<object> already)=>DynamicData.Read(input,already);
}