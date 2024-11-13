namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	public static string Stringify(object? value,bool pretty)=>From(value).ToString(pretty);

	#region Cast
	public static T Cast<T>(object? source)=>From(source).To<T>()!;
	public static object? Cast(object? source,Type type)=>From(source).To(type);
	public static bool TryCast<T>(object? source,out T t,bool throwOnError=false)=>From(source).TryTo(out t!,throwOnError);
	public static bool TryCast(object? source,Type type,out object? obj,bool throwOnError=false)=>From(source).TryTo(type,out obj,throwOnError);
	#endregion

	internal static readonly Dictionary<Type,RpcData.ToFunc> ToDictionary=new();
	internal static readonly List<RpcData.ToFunc> ToList=[];

	public T? To<T>()=>(T?)To(typeof(T));

	public object? To(Type type)
		=>TryTo(type,out var obj,true)
			  ?obj
			  :throw new InvalidCastException("Error converting primitive "+this+" to "+RpcTypeStringifier.FromType(type));

	public bool TryTo<T>(out T? t,bool throwOnError=false){
		if(TryTo(typeof(T),out var obj,throwOnError)){
			t=(T?)obj;
			return true;
		}
		t=default;
		return false;
	}

	public bool TryTo(Type type,out object? result,bool throwOnError=false){
		if(RpcData.GetForOutput(ToDictionary,type) is{} fromDict){
			result=fromDict(this,type,throwOnError);
			if(result!=RpcData.ContinueWithNext) return true;
		} else
			foreach(var func in ToList){
				result=func(this,type,throwOnError);
				if(result!=RpcData.ContinueWithNext) return true;
			}
		result=null!;
		if(throwOnError)
			throw new InvalidCastException("Error converting primitive "+this+" to "+RpcTypeStringifier.FromType(type));
		return false;
	}
}