using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	private static readonly Dictionary<Type,Func<object,Dictionary<object,RpcDataPrimitive>,RpcDataPrimitive>> FromDictionary=new();
	private static readonly List<Func<object,Dictionary<object,RpcDataPrimitive>,RpcDataPrimitive?>> FromList=[];

	public static RpcDataPrimitive From(object? value)=>From(value,new Dictionary<object,RpcDataPrimitive>());

	public static RpcDataPrimitive From(object? value,Dictionary<object,RpcDataPrimitive> already){
		if(value switch{
			   null=>Null,
			   true=>True,
			   false=>False,
			   string s=>String(s),
			   char c=>String(c),

			   byte n=>Number(n),
			   sbyte n=>Number(n),
			   short n=>Number(n),
			   ushort n=>Number(n),
			   int n=>Number(n),
			   uint n=>Number(n),
			   long n=>Number(n),
			   ulong n=>Number(n),
			   float n=>Number(n),
			   double n=>Number(n),
			   //decimal n=>RpcDataPrimitive.Number(n),

			   RpcDataPrimitive p=>p,
			   _=>(RpcDataPrimitive?)null,
		   } is{} simple) return simple;
		if(value==null) return Null;//Don't know why, but somehow the null check from before didn't count properly for nullability checks

		if(already.TryGetValue(value,out var alreadyFound)) return alreadyFound;

		if(FromDictionary.TryGetValue(value.GetType(),out var fromType)) return fromType(value,already);

		foreach(var func in FromList)
			if(func(value,already).TryGet(out var primitive))
				return primitive;
		throw new InvalidCastException("Can't convert "+value+" of type "+RpcDataTypeStringifier.FromType(value.GetType())+" to primitive");//TODO maybe return null instead
	}
}