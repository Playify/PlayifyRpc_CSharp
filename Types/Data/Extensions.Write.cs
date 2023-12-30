using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{
	public static void WriteException(this DataOutput output,Exception e){
		if(e is not RpcException remote) remote=new RpcException(e);
		output.WriteString(remote.Type);
		output.WriteString(remote.From);
		output.WriteString(remote.Message);
		output.WriteString(remote.StackTrace);
	}

	public static void WriteDynamic(this DataOutput output,object? d)=>output.WriteDynamic(d,new List<object>());
	public static void WriteDynamic(this DataOutput output,object? d,List<object> already)=>DynamicData.Write(output,d,already);
}