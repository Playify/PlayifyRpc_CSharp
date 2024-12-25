using System.Numerics;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	public class Already:Dictionary<object,RpcDataPrimitive>,IDisposable{
		private readonly List<Action> _disposeList=[];

		public event Action OnDispose{
			add=>_disposeList.Add(value);
			remove=>_disposeList.Remove(value);
		}

		public bool NeedsDispose=>_disposeList.Count!=0;
		public void Dispose()=>_disposeList.ForEach(a=>a());
	}


	internal static readonly Dictionary<Type,RpcData.ObjectToPrimitive> FromDictionary=new();
	internal static readonly List<RpcData.ObjectToPrimitiveOrNull> FromList=[];

	public static RpcDataPrimitive[] FromArray(object?[] values,Already already)
		=>values.Select(v=>v is RpcDataPrimitive p?p:From(v,already)).ToArray();

	public static RpcDataPrimitive From(object? value,Already? already){
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

		if(already?.TryGetValue(value,out var alreadyFound)??false) return alreadyFound;

		already??=new Already();

		var type=value.GetType();
		if(RpcData.GetForInput(FromDictionary,type) is{} fromDict)
			return fromDict(value,already);
		foreach(var func in FromList)
			if(func(value,already) is{} primitive)
				return primitive;
		throw new InvalidCastException("Can't convert "+value+" of type "+RpcTypeStringifier.FromType(type)+" to primitive");
	}
}