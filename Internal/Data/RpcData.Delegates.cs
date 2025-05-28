using PlayifyRpc.Types.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcData{

	#region From
	public delegate RpcDataPrimitive ObjectToPrimitive(object value,RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer);

	public delegate RpcDataPrimitive GenericToPrimitive<in T>(T value,RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer);

	public delegate RpcDataPrimitive? ObjectToPrimitiveOrNull(object value,RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer);
	#endregion

	#region To
	public delegate object? PrimitiveToType(RpcDataPrimitive primitive,Type type,bool throwOnError,RpcDataTransformerAttribute? transformer);

	public delegate object? PrimitiveToObject(RpcDataPrimitive primitive,bool throwOnError,RpcDataTransformerAttribute? transformer);
	#endregion

	#region ReadWrite
	public delegate RpcDataPrimitive ReadFunc(DataInput data,Dictionary<int,RpcDataPrimitive> already,int index);

	public delegate RpcDataPrimitive ReadFunc<out T>(DataInput data,ReadCustomCreator<T> create);

	public delegate RpcDataPrimitive ReadCustomCreator<in T>(T value);

	public delegate void WriteFunc(DataOutput data,object value,Dictionary<RpcDataPrimitive,int> already);

	public delegate void WriteFunc<in T>(DataOutput data,T value,Dictionary<RpcDataPrimitive,int> already);
	#endregion

}