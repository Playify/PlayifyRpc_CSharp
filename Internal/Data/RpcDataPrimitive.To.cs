using PlayifyRpc.Types.Data;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{

	#region Cast
	public static T Cast<T>(object? source)=>From(source).To<T>()!;
	public static object? Cast(object? source,Type type)=>From(source).To(type);
	public static bool TryCast<T>(object? source,out T t,bool throwOnError=false)=>From(source).TryTo(out t!,throwOnError);
	public static bool TryCast(object? source,Type type,out object? obj,bool throwOnError=false)=>From(source).TryTo(type,out obj,throwOnError);
	#endregion

	internal static readonly Dictionary<Type,RpcData.PrimitiveToType> ToDictionary=new();
	internal static readonly List<RpcData.PrimitiveToType> ToList=[];

	public T? To<T>(RpcDataTransformerAttribute? transformer=null)=>(T?)To(typeof(T),transformer);

	public object? To(Type type,RpcDataTransformerAttribute? transformer=null)
		=>TryTo(type,out var obj,true,transformer)
			  ?obj
			  :throw new InvalidCastException("Error converting primitive "+this+" to "+RpcTypeStringifier.FromType(type));

	public bool TryTo<T>(out T? t,bool throwOnError=false,RpcDataTransformerAttribute? transformer=null){
		if(TryTo(typeof(T),out var obj,throwOnError,transformer)){
			t=(T?)obj;
			return true;
		}
		t=default;
		return false;
	}

	public bool TryTo(Type type,out object? result,bool throwOnError=false,RpcDataTransformerAttribute? transformer=null){
		if(transformer!=null&&transformer.TryTo(this,type,out result,throwOnError) is{} overridden) return overridden;

		if(RpcData.GetForOutput(ToDictionary,type) is{} fromDict){
			result=fromDict(this,type,throwOnError,transformer);
			if(result!=RpcData.ContinueWithNext) return true;
		} else
			foreach(var func in ToList){
				result=func(this,type,throwOnError,transformer);
				if(result!=RpcData.ContinueWithNext) return true;
			}
		if(throwOnError)
			throw new InvalidCastException("Error converting primitive "+this+" to "+RpcTypeStringifier.FromType(type));
		result=null;
		return false;
	}
}