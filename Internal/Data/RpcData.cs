namespace PlayifyRpc.Internal.Data;

public static partial class RpcData{
	public static readonly object ContinueWithNext=new();

	public static bool CanBeNull(Type type){
		if(!type.IsValueType) return true;
		return Nullable.GetUnderlyingType(type)!=null;
	}

	#region Dictionary access
	private static bool TryGetOrGeneric<T>(Dictionary<Type,T> dict,Type type,out T? t)=>dict.TryGetValue(type,out t)||type.IsGenericType&&dict.TryGetValue(type.GetGenericTypeDefinition(),out t);

	//Function inputs = from primitive
	public static T? GetForInput<T>(Dictionary<Type,T> dict,Type type){
		if(TryGetOrGeneric(dict,type,out var result)) return result;
		//Base classes (for now only allow abstract, otherwise everything would match with typeof(object) )
		for(var @base=type.BaseType;@base!=null;@base=@base.BaseType)
			if(@base.IsAbstract&&TryGetOrGeneric(dict,@base,out result))
				return result;
		//Interfaces
		foreach(var @interface in type.GetInterfaces())
			if(TryGetOrGeneric(dict,@interface,out result))
				return result;
		return default;
	}

	//Function outputs = to primitive
	public static T? GetForOutput<T>(Dictionary<Type,T> dict,Type type){
		if(TryGetOrGeneric(dict,type,out var result)) return result;
		//Try abstract base classes (non-abstract ones probably don't know how to create child classes)
		for(var @base=type.BaseType;@base!=null;@base=@base.BaseType)
			if(@base.IsAbstract&&TryGetOrGeneric(dict,@base,out result))
				return result;
		//Interfaces
		foreach(var @interface in type.GetInterfaces())
			if(TryGetOrGeneric(dict,@interface,out result))
				return result;
		return default;
	}
	#endregion

}