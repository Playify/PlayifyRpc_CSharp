using System.Text;
using JetBrains.Annotations;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcData{
	[PublicAPI("Only use if not possible otherwise")]
	public static void RegisterFallback(ObjectToPrimitiveOrNull from,PrimitiveToType? to,RpcTypeStringifier.TypeToStringExact toString){
		RpcDataPrimitive.FromList.Add(from);
		if(to!=null) RpcDataPrimitive.ToList.Add(to);
		RpcTypeStringifier.ToStringList.Add(toString);
	}

	public static void Register<T>(GenericToPrimitive<T>? from,PrimitiveToObject? to,RpcTypeStringifier.TypeToString toString)
		=>Register(typeof(T),
			from==null?null:(o,p)=>from((T)o,p),
			to==null?null:(p,_,throwOnError)=>to(p,throwOnError),
			toString);

	public static void Register(Type type,ObjectToPrimitive? from,PrimitiveToType? to,RpcTypeStringifier.TypeToString toString){
		Register(type,from,to,(_,ts,_,_,_,generics)=>toString(ts,generics));
	}

	public static void Register(Type type,ObjectToPrimitive? from,PrimitiveToType? to,RpcTypeStringifier.TypeToStringExact toString){
		if(from!=null) RpcDataPrimitive.FromDictionary.Add(type,from);
		if(to!=null) RpcDataPrimitive.ToDictionary.Add(type,to);
		RpcTypeStringifier.ToStringDictionary.Add(type,toString);
	}

	#region Custom
	private static ReadFunc RegisterCustomBase<T>(ReadFunc<T> read,WriteFunc writer,
		RpcTypeStringifier.TypeToString toStringType,Func<T,string>? toStringInstance,Action<T>? dispose) where T : notnull{

		Register<T>(
			(p,a)=>{
				if(dispose!=null) a.OnDispose+=()=>dispose(p);
				return a[p]=Create(p);
			},
			(p,_)=>{
				if(p.IsNull()&&CanBeNull(typeof(T))) return null;
				if(p.IsAlready(out T already)) return already;
				if(p.IsCustom(out T custom)) return p.AddAlready(custom);
				return ContinueWithNext;
			},
			toStringType
		);

		return (data,already,index)=>read(data,t=>already[index]=Create(t));

		RpcDataPrimitive Create(T t){
			return new RpcDataPrimitive(t,writer,toStringInstance==null?null:()=>toStringInstance(t));
		}
	}

	internal static WriteFunc RegisterCustom<T>(char dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcTypeStringifier.TypeToString toStringType,Func<T,string>? toStringInstance=null,Action<T>? dispose=null) where T : notnull{

		WriteFunc writer=(data,t,already)=>{
			data.WriteLength(dataId);
			write(data,(T)t,already);
		};
		RpcDataPrimitive.ReadByChar.Add(dataId,RegisterCustomBase(read,writer,toStringType,toStringInstance,dispose));
		return writer;
	}

	[PublicAPI]
	public static WriteFunc RegisterCustom<T>(string dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcTypeStringifier.TypeToString toStringType,Func<T,string>? toStringInstance=null,Action<T>? dispose=null) where T : notnull{

		var bytes=Encoding.UTF8.GetBytes(dataId);
		WriteFunc writer=(data,t,already)=>{
			data.WriteLength(bytes.Length+128);
			data.Write(bytes);
			write(data,(T)t,already);
		};
		RpcDataPrimitive.ReadByString.Add(dataId,RegisterCustomBase(read,writer,toStringType,toStringInstance,dispose));
		return writer;
	}
	#endregion

}