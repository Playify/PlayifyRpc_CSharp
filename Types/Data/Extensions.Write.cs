using PlayifyRpc.Internal.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data;

public static partial class Extensions{
	public static void WriteException(this DataOutput output,Exception e)=>RpcException.WrapAndFreeze(e).Write(output);
	public static void WriteDynamic(this DataOutputBuff output,object? d)=>output.WriteDynamic(d,new Dictionary<object,int>());
	public static void WriteDynamic(this DataOutputBuff output,object? d,Dictionary<object,int> already)=>DynamicData.Write(output,d,already);
}