using System.Text;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	static RpcDataPrimitive(){
		RpcSetupAttribute.LoadAll();
	}

	internal static readonly Type[] ValueTupleTypes=[
		typeof(ValueTuple),
		typeof(ValueTuple<>),
		typeof(ValueTuple<,>),
		typeof(ValueTuple<,,>),
		typeof(ValueTuple<,,,>),
		typeof(ValueTuple<,,,,>),
		typeof(ValueTuple<,,,,,>),
		typeof(ValueTuple<,,,,,,>),
		typeof(ValueTuple<,,,,,,,>),
	];

	public static void RegisterGeneric(
		Func<object,Dictionary<object,RpcDataPrimitive>,RpcDataPrimitive?> get,
		Func<RpcDataPrimitive,Type,object?> convert,
		RpcDataTypeStringifier.GeneralDelegate toString
	){
		FromList.Add(get);
		ToList.Add(convert);
		RpcDataTypeStringifier.ToStringList.Add(toString);
	}

	public static void Register<T>(
		Func<T,Dictionary<object,RpcDataPrimitive>,RpcDataPrimitive>? get,
		Func<RpcDataPrimitive,object?> convert,
		RpcDataTypeStringifier.TypedDelegate toString
	){
		ToDictionary.Add(typeof(T),convert);
		if(get!=null) FromDictionary.Add(typeof(T),(o,p)=>get((T)o,p));
		RpcDataTypeStringifier.ToStringDictionary.Add(typeof(T),toString);
	}

	public static void RegisterObject<T>(
		Func<T,IEnumerable<(string key,object? value)>> getProps,
		Func<T,IEnumerable<(string key,RpcDataPrimitive value)>,bool> setProps,
		RpcDataTypeStringifier.TypedDelegate toString,
		bool allowNull
	) where T : notnull,new()
		=>Register<T>(
			(p,a)=>a[p]=Object(()=>getProps(p).Select(t=>(t.key,From(t.value)))),
			p=>{
				if(allowNull&&p.IsNull()) return null;
				if(p.IsAlready(out T already)) return already;
				if(!p.IsObject(out var props)) return ContinueWithNext;
				var obj=p.AddAlready(new T());
				if(setProps(obj,props)) return obj;
				p.RemoveAlready(obj);
				return ContinueWithNext;
			},
			toString
		);

	private static void RegisterCustom<T>(
		Action<DataOutputBuff,T,Dictionary<RpcDataPrimitive,int>> write,
		RpcDataTypeStringifier.TypedDelegate toString
	) where T : notnull
		=>Register<T>(
			(p,a)=>a[p]=Custom(p,write),
			p=>{
				if(p.IsNull()){
					var type=typeof(T);
					if(!type.IsValueType||Nullable.GetUnderlyingType(type)!=null)
						return null;
				}
				if(p.IsAlready(out T already)) return already;
				if(p.IsCustom(out T custom)) return p.AddAlready(custom);
				return ContinueWithNext;
			},
			toString
		);

	public static void RegisterCustom<T>(
		string dataId,
		Func<DataInput,Func<T,T>,T> read,
		Action<DataOutput,T,Dictionary<RpcDataPrimitive,int>> write,
		RpcDataTypeStringifier.TypedDelegate toString
	) where T : notnull{
		RegisterCustom<T>((data,t,already)=>{
			var bytes=Encoding.UTF8.GetBytes(dataId);
			data.WriteLength(bytes.Length+128);
			data.Write(bytes);
			write(data,t,already);
		},toString);
		//TODO DynamicData.Register(dataId,read,write);
	}

	internal static void RegisterCustom<T>(
		char dataId,
		Func<DataInput,Func<T,T>,T> read,
		Action<DataOutput,T,Dictionary<RpcDataPrimitive,int>> write,
		RpcDataTypeStringifier.TypedDelegate toString
	) where T : notnull{
		RegisterCustom<T>((data,t,already)=>{
			data.WriteLength(dataId);
			write(data,t,already);
		},toString);
		//TODO DynamicData.Register(dataId,read,write);
	}
}