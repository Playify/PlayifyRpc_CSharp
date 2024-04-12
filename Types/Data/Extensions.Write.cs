using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{
	public static void WriteException(this DataOutput output,Exception e)=>RpcException.Convert(e,true).Write(output);
	public static void WriteDynamic(this DataOutput output,object? d)=>output.WriteDynamic(d,new List<object>());
	public static void WriteDynamic(this DataOutput output,object? d,List<object> already)=>DynamicData.Write(output,d,already);
}