namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	public static string Stringify(object? value,bool pretty)=>From(value).ToString(pretty);

	#region Cast
	public static T Cast<T>(object? source)=>From(source).To<T>();
	public static object? Cast(object? source,Type type)=>From(source).To(type);
	public static bool TryCast<T>(object? source,out T t)=>From(source).TryTo(out t);
	public static bool TryCast(object? source,Type type,out object? obj)=>From(source).TryTo(type,out obj);

	//TODO implement throwOnError
	public static bool TryCast(object? source,Type type,out object? obj,bool throwOnError)=>From(source).TryTo(type,out obj);
	public static bool TryCast<T>(object? source,out T t,bool throwOnError)=>From(source).TryTo(out t);
	#endregion

	public delegate object? ToFunc(RpcDataPrimitive primitive,Type type);

	private static readonly Dictionary<Type,ToFunc> ToDictionary=new();
	private static readonly List<ToFunc> ToList=[RpcDataDefaults.ToNullable];

	public T To<T>()=>(T)To(typeof(T))!;

	public object? To(Type type)
		=>TryTo(type,out var obj)
			  ?obj
			  :throw new InvalidCastException("Error converting primitive "+this+" to "+RpcDataTypeStringifier.FromType(type));

	public bool TryTo<T>(out T t){
		if(TryTo(typeof(T),out var obj)){
			t=(T)obj!;
			return true;
		}
		t=default!;
		return false;
	}

	public bool TryTo(Type type,out object? result){
		if(ToDictionary.TryGetValue(type,out var fromType)){
			result=fromType(this,type);
			if(result!=ContinueWithNext) return true;
		}
		if(type.IsGenericType&&ToDictionary.TryGetValue(type.GetGenericTypeDefinition(),out fromType)){
			result=fromType(this,type);
			if(result!=ContinueWithNext) return true;
		}
		foreach(var func in ToList){
			result=func(this,type);
			if(result!=ContinueWithNext) return true;
		}
		result=null!;
		return false;
	}
}