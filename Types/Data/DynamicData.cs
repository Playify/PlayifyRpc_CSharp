using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using PlayifyUtility.Jsons;
using PlayifyUtility.Streams.Data;
#if NETFRAMEWORK
using PlayifyUtility.Utils.Extensions;
#endif

namespace PlayifyRpc.Types.Data;

[PublicAPI]
public static class DynamicData{
	private static readonly List<(string id,Predicate<object> check,Action<DataOutput,object,List<object>> write)> WriteRegistry=new();
	private static readonly Dictionary<string,Func<DataInput,List<object>,object>> ReadRegistry=new();

	static DynamicData(){
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);
	}

	internal static object? Read(DataInput incoming,List<object> already){
		var objectId=incoming.ReadLength();

		if(objectId<0){
			//Already, String, Object, Array
			objectId=-objectId;
			switch(objectId%4){
				case 0:return already[objectId/4];
				case 1:return Encoding.UTF8.GetString(incoming.ReadFully(objectId/4));
				case 2:{
					var o=new ExpandoObject();
					already.Add(o);
					for(var i=0;i<objectId/4;i++){
						var key=incoming.ReadString()!;
						var value=Read(incoming,already);
						((IDictionary<string,object?>)o)[key]=value;
					}
					return o;
				}
				case 3:{
					var o=new object?[objectId/4];
					already.Add(o);
					for(var i=0;i<o.Length;i++) o[i]=Read(incoming,already);
					return o;
				}
			}
			throw new Exception("Unreachable code reached");
		}
		if(objectId>=128){
			var type=Encoding.UTF8.GetString(incoming.ReadFully(objectId-128));
			if(ReadRegistry.TryGetValue(type,out var read))
				return read(incoming,already);
			throw new ArgumentException();
		}
		if(objectId=='F'){
			var func=new RpcFunction(incoming.ReadString()??throw new NullReferenceException(),incoming.ReadString()??throw new NullReferenceException());
			already.Add(func);
			return func;
		}

		return objectId switch{
			'n'=>null,
			't'=>true,
			'f'=>false,
			'i'=>incoming.ReadInt(),
			'd'=>incoming.ReadDouble(),
			'l'=>incoming.ReadLong(),
			//'s'=>incoming.ReadString(),
			//'a'=>already[incoming.ReadInt()],
			'b'=>incoming.ReadFully(incoming.ReadLength()),
			'D'=>DateTimeOffset.FromUnixTimeMilliseconds(incoming.ReadLong()).LocalDateTime,
			'R'=>new Regex(incoming.ReadString()??"",(RegexOptions)incoming.ReadByte()),
			'E'=>incoming.ReadException(),
			'O'=>new RpcObject(incoming.ReadString()??throw new NullReferenceException()),
			_=>throw new ArgumentException(),
		};
	}

	internal static void Write(DataOutput output,object? d,List<object> already){
		d=d switch{
			JsonString j=>j.Value,
			JsonNumber j=>j.Value,
			JsonBool j=>j.Value,
			JsonNull=>null,
			_=>d,
		};


		switch(d){
			case null:
				output.WriteLength('n');
				return;
			case true:
				output.WriteLength('t');
				return;
			case false:
				output.WriteLength('f');
				return;
			case byte:
			case sbyte:
			case short:
			case ushort:
			case int:
			case uint and <int.MaxValue:
			case Enum:
				output.WriteLength('i');
				output.WriteInt(Convert.ToInt32(d));
				return;
			case uint:
			case float:
			case double:
			case decimal:
				output.WriteLength('d');
				output.WriteDouble(Convert.ToDouble(d));
				return;
			case long:
			case ulong:
				output.WriteLength('l');
				output.WriteLong(Convert.ToInt64(d));
				return;
			case byte[] buffer:
				output.WriteLength('b');
				output.WriteLength(buffer.Length);
				output.Write(buffer);
				return;
			case DateTime dateTime:
				output.WriteLength('D');
				output.WriteLong(new DateTimeOffset(dateTime).ToUnixTimeMilliseconds());
				return;
			case Regex regex:
				output.WriteLength('R');
				output.WriteString(regex.ToString());
				output.WriteByte((byte)(regex.Options&(RegexOptions)3));
				return;
			case Exception exception:
				output.WriteLength('E');
				output.WriteException(exception);
				return;
			case RpcObject obj:
				output.WriteLength('O');
				output.WriteString(obj.Type);
				return;
		}

		var index=already.IndexOf(d);
		if(index!=-1){
			output.WriteLength(-(index*4+0));
			return;
		}

		switch(d){
			case RpcFunction func:
				already.Add(func);
				output.WriteLength('F');
				output.WriteString(func.Type);
				output.WriteString(func.Method);
				return;
			case Delegate func:
				already.Add(func);
				var rpcFunc=RpcFunction.RegisterFunction(func);
				output.WriteLength('F');
				output.WriteString(rpcFunc.Type);
				output.WriteString(rpcFunc.Method);
				return;
			case string s:
				var bytes=Encoding.UTF8.GetBytes(s);
				output.WriteLength(-(bytes.Length*4+1));
				output.Write(bytes);
				return;
			case ObjectTemplate obj:
				obj.WriteDynamic(output,already);
				return;
			case ExpandoObject obj:
				already.Add(obj);
				var dict=(IDictionary<string,object?>)obj;
				output.WriteLength(-(dict.Count*4+2));
				foreach(var (key,value) in dict){
					output.WriteString(key);
					Write(output,value,already);
				}
				return;
			case JsonObject obj:
				already.Add(obj);
				output.WriteLength(-(obj.Count*4+2));
				foreach(var (key,value) in obj){
					output.WriteString(key);
					Write(output,value,already);
				}
				return;
			case Array arr:
				already.Add(arr);
				output.WriteLength(-(arr.Length*4+3));
				foreach(var o in arr) Write(output,o,already);
				return;
			case ITuple arr:
				already.Add(arr);
				output.WriteLength(-(arr.Length*4+3));
				for(var i=0;i<arr.Length;i++) Write(output,arr[i],already);
				return;
			case JsonArray arr:
				already.Add(arr);
				output.WriteLength(-(arr.Count*4+3));
				foreach(var o in arr) Write(output,o,already);
				return;
		}
		foreach(var (id,check,write) in WriteRegistry){
			if(!check(d)) continue;
			var idBytes=Encoding.UTF8.GetBytes(id);
			output.WriteLength(idBytes.Length+128);
			output.Write(idBytes);
			write(output,d,already);
			return;
		}
		throw new Exception("Unknown Type: "+d.GetType().Name+" for "+d);
	}

	private static void RegisterAssembly(Assembly assembly){
		foreach(var type in assembly.GetTypes()){
			var remoteClass=type.GetCustomAttribute<CustomDynamicTypeAttribute>();
			if(remoteClass==null) continue;

			/*
			var read=type.GetMethod("Read",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(DataInput),typeof(List<object>)},null);
			read??=type.GetMethod("Read",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(DataInput)},null);*/
			var readConstructor=type.GetConstructor(new[]{typeof(DataInput),typeof(List<object>)});
			Func<DataInput,List<object>,object> read;
			if(readConstructor==null){
				readConstructor=type.GetConstructor(new[]{typeof(DataInput)});
				if(readConstructor==null) throw new Exception("Type "+type+" does not implement a public constructor(DataInput i,List<object> already) or public constructor(DataInput i)");

				read=(i,_)=>readConstructor.Invoke(new object[]{i});
			} else read=(i,already)=>readConstructor.Invoke(new object[]{i,already});


			var writeMethod=type.GetMethod("Write",BindingFlags.Public|BindingFlags.Instance,null,new[]{typeof(DataOutput),typeof(List<object>)},null);
			Action<DataOutput,object,List<object>> write;
			if(writeMethod==null){
				writeMethod=type.GetMethod("Write",BindingFlags.Public|BindingFlags.Instance,null,new[]{typeof(DataOutput)},null);
				if(writeMethod==null) throw new Exception("Type "+type+" does not implement a public void Write(DataOutput o,List<object> already) or public void Write(DataOutput o) method");

				write=(o,d,_)=>writeMethod.Invoke(d,new object[]{o});
			} else write=(o,d,already)=>writeMethod.Invoke(d,new object[]{o,already});


			ReadRegistry.Add(remoteClass.Id,read);
			WriteRegistry.Add((remoteClass.Id,type.IsInstanceOfType,write));
		}
	}

	public static void Register(string id,Func<DataInput,List<object>,object> read,Predicate<object> check,Action<DataOutput,object,List<object>> write){
		ReadRegistry.Add(id,read);
		WriteRegistry.Add((id,check,write));
	}

	public static void Register(string id,Predicate<object> check,Action<DataOutput,object,List<object>> write)=>WriteRegistry.Add((id,check,write));

	public static void Register<T>(string id,Func<DataInput,List<object>,T> read,Action<DataOutput,T,List<object>> write)
		=>Register(
			id,
			(data,already)=>read(data,already)!,
			o=>o is T,
			(data,o,already)=>write(data,(T)o,already)
		);

	public static void Free(List<object> already){
		foreach(var d in already.OfType<Delegate>()) RpcFunction.UnregisterFunction(d);
	}
}