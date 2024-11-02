using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcData{

	#region From
	public delegate RpcDataPrimitive FromFunc(object value,Dictionary<object,RpcDataPrimitive> already);

	public delegate RpcDataPrimitive FromFunc<in T>(T value,Dictionary<object,RpcDataPrimitive> already);

	public delegate RpcDataPrimitive? FromFuncMaybe(object value,Dictionary<object,RpcDataPrimitive> already);
	#endregion

	#region To
	public delegate object? ToFunc(RpcDataPrimitive primitive,Type type,bool throwOnError);
	#endregion

	#region ReadWrite
	public delegate RpcDataPrimitive ReadFunc(DataInput data,Dictionary<int,RpcDataPrimitive> already,int index);

	public delegate RpcDataPrimitive ReadFunc<out T>(DataInput data,ReadCustomCreator<T> create);

	public delegate RpcDataPrimitive ReadCustomCreator<in T>(T value,bool addAlready);

	public delegate void WriteFunc(DataOutput data,object value,Dictionary<RpcDataPrimitive,int> already);

	public delegate void WriteFunc<in T>(DataOutput data,T value,Dictionary<RpcDataPrimitive,int> already);
	#endregion

}