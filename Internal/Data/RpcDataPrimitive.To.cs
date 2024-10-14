namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{

	#region Cast
	public static T Cast<T>(object? source)=>From(source).To<T>();
	public static object? Cast(object? source,Type type)=>From(source).To(type);
	public static bool TryCast<T>(object? source,out T t)=>From(source).TryTo(out t);
	public static bool TryCast(object? source,Type type,out object? obj)=>From(source).TryTo(type,out obj);
	#endregion

	public static readonly Dictionary<Type,Func<RpcDataPrimitive,object?>> ToDictionary=new();
	public static readonly List<Func<RpcDataPrimitive,Type,object?>> ToList=[
		RpcDataDefaults.ToNullable,
	];
	public static readonly object ContinueWithNext=new();

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
			result=fromType(this);
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