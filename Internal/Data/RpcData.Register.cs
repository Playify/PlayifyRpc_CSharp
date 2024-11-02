using System.Text;
using JetBrains.Annotations;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcData{
	[PublicAPI("Only use if not possible otherwise")]
	public static void RegisterFallback(FromFuncMaybe from,ToFunc? to,RpcTypeStringifier.UnknownFunc toString){
		RpcDataPrimitive.FromList.Add(from);
		if(to!=null) RpcDataPrimitive.ToList.Add(to);
		RpcTypeStringifier.ToStringList.Add(toString);
	}

	public static void Register<T>(FromFunc<T>? from,Func<RpcDataPrimitive,bool,object?>? to,RpcTypeStringifier.KnownFunc toString)
		=>Register(typeof(T),
			from==null?null:(o,p)=>from((T)o,p),
			to==null?null:(p,_,throwOnError)=>to(p,throwOnError),
			toString);

	public static void Register(Type type,FromFunc? from,ToFunc? to,RpcTypeStringifier.KnownFunc toString){
		Register(type,from,to,(_,ts,_,_,_,generics)=>toString(ts,generics));
	}

	public static void Register(Type type,FromFunc? from,ToFunc? to,RpcTypeStringifier.UnknownFunc toString){
		if(from!=null) RpcDataPrimitive.FromDictionary.Add(type,from);
		if(to!=null) RpcDataPrimitive.ToDictionary.Add(type,to);
		RpcTypeStringifier.ToStringDictionary.Add(type,toString);
	}

	#region Custom
	private static void RegisterCustomBase<T>(WriteFunc writer,RpcTypeStringifier.KnownFunc toString,Action<T>? dispose=null) where T : notnull{
		Register<T>(
			(p,a)=>a[p]=new RpcDataPrimitive(p,writer,dispose==null?null:()=>dispose(p)),
			(p,_)=>{
				if(p.IsNull()&&CanBeNull(typeof(T))) return null;
				if(p.IsAlready(out T already)) return already;
				if(p.IsCustom(out T custom)) return p.AddAlready(custom);
				return ContinueWithNext;
			},
			toString
		);
	}

	internal static WriteFunc RegisterCustom<T>(char dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcTypeStringifier.KnownFunc toString,Action<T>? dispose=null) where T : notnull{

		WriteFunc writer=(data,t,already)=>{
			data.WriteLength(dataId);
			write(data,(T)t,already);
		};
		RegisterCustomBase(writer,toString,dispose);
		RpcDataPrimitive.ReadByChar.Add(dataId,(data,already,index)=>{
			return read(data,(t,addAlready)=>{
				var custom=new RpcDataPrimitive(t,writer,dispose==null?null:()=>dispose(t));
				if(addAlready) already[index]=custom;
				return custom;
			});
		});
		return writer;
	}

	[PublicAPI]
	public static void RegisterCustom<T>(string dataId,ReadFunc<T> read,WriteFunc<T> write,
		RpcTypeStringifier.KnownFunc toString,Action<T>? dispose=null) where T : notnull{

		var bytes=Encoding.UTF8.GetBytes(dataId);
		WriteFunc writer=(data,t,already)=>{
			data.WriteLength(bytes.Length+128);
			data.Write(bytes);
			write(data,(T)t,already);
		};
		RegisterCustomBase(writer,toString,dispose);
		RpcDataPrimitive.ReadByString.Add(dataId,(data,already,index)=>{
			return read(data,(t,addAlready)=>{
				var custom=new RpcDataPrimitive(t,writer,dispose==null?null:()=>dispose(t));
				if(addAlready) already[index]=custom;
				return custom;
			});
		});
	}
	#endregion

}