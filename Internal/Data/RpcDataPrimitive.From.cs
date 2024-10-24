using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	public delegate RpcDataPrimitive FromFunc(object value,Dictionary<object,RpcDataPrimitive> already);

	public delegate RpcDataPrimitive FromFunc<in T>(T value,Dictionary<object,RpcDataPrimitive> already);

	public delegate RpcDataPrimitive? FromFuncMaybe(object value,Dictionary<object,RpcDataPrimitive> already);

	private static readonly Dictionary<Type,FromFunc> FromDictionary=new();
	private static readonly List<FromFuncMaybe> FromList=[];

	public static RpcDataPrimitive[] FromArray(object?[] values){
		Dictionary<object,RpcDataPrimitive>? already=null;
		return values.Select(v=>v is RpcDataPrimitive p?p:From(v,already??=new Dictionary<object,RpcDataPrimitive>())).ToArray();
	}

	public static RpcDataPrimitive From(object? value)=>value is RpcDataPrimitive p?p:From(value,new Dictionary<object,RpcDataPrimitive>());

	public static RpcDataPrimitive From(object? value,Dictionary<object,RpcDataPrimitive> already){
		if(value switch{
			   null=>new RpcDataPrimitive(),
			   true=>new RpcDataPrimitive(true),
			   false=>new RpcDataPrimitive(false),
			   string s=>new RpcDataPrimitive(s),
			   char c=>new RpcDataPrimitive(c),

			   byte n=>new RpcDataPrimitive(n),
			   sbyte n=>new RpcDataPrimitive(n),
			   short n=>new RpcDataPrimitive(n),
			   ushort n=>new RpcDataPrimitive(n),
			   int n=>new RpcDataPrimitive(n),
			   uint n=>new RpcDataPrimitive(n),
			   long n=>new RpcDataPrimitive(n),
			   ulong n=>new RpcDataPrimitive(n),
			   float n=>new RpcDataPrimitive(n),
			   double n=>new RpcDataPrimitive(n),
			   //decimal n=>RpcDataPrimitive.Number(n),

			   RpcDataPrimitive p=>p,
			   _=>(RpcDataPrimitive?)null,
		   } is{} simple) return simple;
		if(value==null) return new RpcDataPrimitive();//Don't know why, but somehow the null check from before didn't count properly for nullability checks

		if(already.TryGetValue(value,out var alreadyFound)) return alreadyFound;

		var type=value.GetType();
		if(FromDictionary.TryGetValue(type,out var fromType))
			return fromType(value,already);
		if(type.IsGenericType&&FromDictionary.TryGetValue(type.GetGenericTypeDefinition(),out fromType))
			return fromType(value,already);

		foreach(var func in FromList)
			if(func(value,already).TryGet(out var primitive))
				return primitive;
		throw new InvalidCastException("Can't convert "+value+" of type "+RpcDataTypeStringifier.FromType(type)+" to primitive");
	}
}