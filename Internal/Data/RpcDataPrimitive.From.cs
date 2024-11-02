using System.Numerics;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	internal static readonly Dictionary<Type,RpcData.FromFunc> FromDictionary=new();
	internal static readonly List<RpcData.FromFuncMaybe> FromList=[];

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
			   char c=>new RpcDataPrimitive(char.ToString(c)),

			   byte n=>new RpcDataPrimitive(n),
			   sbyte n=>new RpcDataPrimitive(n),
			   short n=>new RpcDataPrimitive(n),
			   ushort n=>new RpcDataPrimitive(n),
			   int n=>new RpcDataPrimitive(n),
			   uint n=>new RpcDataPrimitive(n),
			   long n=>new RpcDataPrimitive(new BigInteger(n)),
			   ulong n=>new RpcDataPrimitive(new BigInteger(n)),
			   BigInteger n=>new RpcDataPrimitive(n),
			   float n=>new RpcDataPrimitive(n),
			   double n=>new RpcDataPrimitive(n),

			   RpcDataPrimitive p=>p,
			   _=>(RpcDataPrimitive?)null,
		   } is{} simple) return simple;
		if(value==null) return new RpcDataPrimitive();//C# doesn't recognize the nullcheck from before

		if(already.TryGetValue(value,out var alreadyFound)) return alreadyFound;
		
		var type=value.GetType();
		if(RpcData.GetForInput(FromDictionary,type) is{} fromDict)
			return fromDict(value,already);
		foreach(var func in FromList)
			if(func(value,already).TryGet(out var primitive))
				return primitive;
		throw new InvalidCastException("Can't convert "+value+" of type "+RpcTypeStringifier.FromType(type)+" to primitive");
	}
}