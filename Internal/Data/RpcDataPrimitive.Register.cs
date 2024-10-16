using System.Text;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{

	static RpcDataPrimitive(){
		RpcSetupAttribute.LoadAll();
	}

	private static bool CanBeNull(Type type){
		if(!type.IsValueType) return true;
		return Nullable.GetUnderlyingType(type)!=null;
	}

	public static void RegisterFallback(FromFuncMaybe from,ToFunc to,RpcDataTypeStringifier.UnknownFunc toString){
		FromList.Add(from);
		ToList.Add(to);
		RpcDataTypeStringifier.ToStringList.Add(toString);
	}

	public static void Register<T>(FromFunc<T>? from,Func<RpcDataPrimitive,object?> to,RpcDataTypeStringifier.KnownFunc toString)
		=>Register(typeof(T),from==null?null:(o,p)=>from((T)o,p),(p,_)=>to(p),toString);

	public static void Register(Type type,FromFunc? from,ToFunc to,RpcDataTypeStringifier.KnownFunc toString){
		if(from!=null) FromDictionary.Add(type,from);
		ToDictionary.Add(type,to);
		RpcDataTypeStringifier.ToStringDictionary.Add(type,toString);
	}

	#region Object
	public static void RegisterObject<T>(
		Func<T,IEnumerable<(string key,object? value)>> getProps,
		Func<T,IEnumerable<(string key,RpcDataPrimitive value)>,bool> setProps,
		RpcDataTypeStringifier.KnownFunc toString
	) where T : notnull,new()=>Register<T>(
		(p,a)=>a[p]=new RpcDataPrimitive(()=>getProps(p).Select(t=>(t.key,From(t.value)))),
		p=>{
			if(p.IsNull()&&CanBeNull(typeof(T))) return null;
			if(p.IsAlready(out T already)) return already;
			if(!p.IsObject(out var props)) return ContinueWithNext;
			var obj=p.AddAlready(new T());
			return setProps(obj,props)?obj:p.RemoveAlready(obj);
		},
		toString
	);
	#endregion

	#region Custom
	internal static void RegisterCustomBase<T>(WriteFunc writer,RpcDataTypeStringifier.KnownFunc toString,Action<T>? dispose=null) where T : notnull{
		Register<T>(
			(p,a)=>a[p]=new RpcDataPrimitive(p,writer,dispose==null?null:()=>dispose(p)),
			p=>{
				if(p.IsNull()&&CanBeNull(typeof(T))) return null;
				if(p.IsAlready(out T already)) return already;
				if(p.IsCustom(out T custom)) return p.AddAlready(custom);
				return ContinueWithNext;
			},
			toString
		);
	}

	internal static void RegisterCustom<T>(char dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcDataTypeStringifier.KnownFunc toString,Action<T>? dispose=null) where T : notnull
		=>RegisterCustom(dataId,read,write,toString,dispose,out _);

	internal static void RegisterCustom<T>(char dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcDataTypeStringifier.KnownFunc toString,Action<T>? dispose,out WriteFunc writer) where T : notnull{

		var localCopy=writer=(data,t,already)=>{
			data.WriteLength(dataId);
			write(data,(T)t,already);
		};
		RegisterCustomBase(writer,toString,dispose);
		ReadByChar.Add(dataId,(data,already,index)=>{
			return read(data,(t,addAlready)=>{
				var custom=new RpcDataPrimitive(t,localCopy,dispose==null?null:()=>dispose(t));
				if(addAlready) already[index]=custom;
				return custom;
			});
		});
	}

	public static void RegisterCustom<T>(string dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcDataTypeStringifier.KnownFunc toString,Action<T>? dispose=null) where T : notnull{

		var bytes=Encoding.UTF8.GetBytes(dataId);
		WriteFunc writer=(data,t,already)=>{
			data.WriteLength(bytes.Length+128);
			data.Write(bytes);
			write(data,(T)t,already);
		};
		RegisterCustomBase(writer,toString,dispose);
		ReadByString.Add(dataId,(data,already,index)=>{
			return read(data,(t,addAlready)=>{
				var custom=new RpcDataPrimitive(t,writer,dispose==null?null:()=>dispose(t));
				if(addAlready) already[index]=custom;
				return custom;
			});
		});
	}
	#endregion

}