using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Jsons;
using PlayifyUtility.Streams.Data;
using PlayifyUtility.Utils.Extensions;
#if NETFRAMEWORK
using PlayifyUtility.Utils.Extensions;
#endif

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public static class DynamicData{
	private static readonly List<(string id,Predicate<object> check,Action<DataOutput,object,Dictionary<object,int>> write)> WriteRegistry=[];
	private static readonly Dictionary<string,Func<DataInput,Func<object,object>,object>> ReadRegistry=new();
	private static readonly List<Func<object?,object?>> Converters=[
		d=>d switch{
			JsonString j=>j.Value,
			JsonNumber j=>j.Value,
			JsonBool j=>j.Value,
			JsonNull=>null,
			_=>d,
		},
		d=>d is char c?c.ToString():d,
	];

	static DynamicData(){
		AppDomain.CurrentDomain.AssemblyLoad+=(_,args)=>RegisterAssembly(args.LoadedAssembly);
		foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) RegisterAssembly(assembly);
	}

	internal static object? Read(DataInputBuff incoming,Dictionary<int,object> already){
		var index=incoming.GetBufferOffsetAndLength().off;

		var objectId=incoming.ReadLength();

		if(objectId<0){
			//Already, String, Object, Array
			objectId=-objectId;
			switch(objectId%4){
				case 0:return already[objectId/4];
				case 1:return AlreadyFunc(Encoding.UTF8.GetString(incoming.ReadFully(objectId/4)));
				case 2:{
					var o=AlreadyFunc(new ExpandoObject());
					for(var i=0;i<objectId/4;i++){
						var key=incoming.ReadString()!;
						var value=Read(incoming,already);
						((IDictionary<string,object?>)o)[key]=value;
					}
					return o;
				}
				case 3:{
					var o=AlreadyFunc(new object?[objectId/4]);
					for(var i=0;i<o.Length;i++) o[i]=Read(incoming,already);
					return o;
				}
			}
			throw new Exception("Unreachable code reached");
		}
		if(objectId>=128){
			var type=Encoding.UTF8.GetString(incoming.ReadFully(objectId-128));
			if(ReadRegistry.TryGetValue(type,out var read))
				return read(incoming,AlreadyFunc);
			throw new ArgumentException();
		}
		return objectId switch{
			'n'=>null,
			't'=>true,
			'f'=>false,
			'i'=>incoming.ReadInt(),
			'd'=>incoming.ReadDouble(),
			'l'=>incoming.ReadLong(),
			'b'=>AlreadyFunc(incoming.ReadFully(incoming.ReadLength())),
			'D'=>DateTimeOffset.FromUnixTimeMilliseconds(incoming.ReadLong()).LocalDateTime,
			'R'=>AlreadyFunc(new Regex(incoming.ReadString()??"",(RegexOptions)incoming.ReadByte())),
			'E'=>AlreadyFunc(incoming.ReadException()),
			'O'=>AlreadyFunc(new RpcObject(incoming.ReadString()??throw new NullReferenceException())),
			'F'=>AlreadyFunc(new RpcFunction(incoming.ReadString()??throw new NullReferenceException(),incoming.ReadString()??throw new NullReferenceException())),
			_=>throw new ArgumentException(),
		};

		T AlreadyFunc<T>(T t) where T : notnull{
			already[index]=t;
			return t;
		}
	}

	internal static void Write(DataOutputBuff output,object? d,Dictionary<object,int> already){
		foreach(var converter in Converters) d=converter(d);

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
			case DateTime dateTime:
				output.WriteLength('D');
				output.WriteLong(new DateTimeOffset(dateTime).ToUnixTimeMilliseconds());
				return;
		}

		if(already.TryGetValue(d,out var index)){
			output.WriteLength(-(index*4+0));
			return;
		}
		already[d]=output.Length;

		switch(d){
			case byte[] buffer:
				output.WriteLength('b');
				output.WriteLength(buffer.Length);
				output.Write(buffer);
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
			case RpcFunction func:
				output.WriteLength('F');
				output.WriteString(func.Type);
				output.WriteString(func.Method);
				return;
			case Delegate func:
				var rpcFunc=RpcFunction.RegisterFunction(func);
				already[rpcFunc]=output.Length;
				output.WriteLength('F');
				output.WriteString(rpcFunc.Type);
				output.WriteString(rpcFunc.Method);
				return;
			case string s:
				var bytes=Encoding.UTF8.GetBytes(s);
				output.WriteLength(-(bytes.Length*4+1));
				output.Write(bytes);
				return;
			case ObjectTemplateBase obj:
				obj.WriteDynamic(output,already);
				return;
			case ExpandoObject obj:
				var dict=(IDictionary<string,object?>)obj;
				output.WriteLength(-(dict.Count*4+2));
				foreach(var (key,value) in dict){
					output.WriteString(key);
					Write(output,value,already);
				}
				return;
			case JsonObject obj:
				output.WriteLength(-(obj.Count*4+2));
				foreach(var (key,value) in obj){
					output.WriteString(key);
					Write(output,value,already);
				}
				return;
			case Array arr:
				output.WriteLength(-(arr.Length*4+3));
				foreach(var o in arr) Write(output,o,already);
				return;
			case ITuple arr:
				output.WriteLength(-(arr.Length*4+3));
				for(var i=0;i<arr.Length;i++) Write(output,arr[i],already);
				return;
			case JsonArray arr:
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
		throw new RpcDataException("Type is not supported by DynamicData: "+DynamicTypeStringifier.FromType(d.GetType())+" for value "+d,null);
	}

	private static void RegisterAssembly(Assembly assembly){
		if(assembly.FullName?.StartsWith("System.")??false) return;//Skip System assemblies

		Debug.WriteLine("DynamicData registering "+assembly);
		try{
			foreach(var type in assembly.GetTypes()){
				var remoteClass=type.GetCustomAttribute<CustomDynamicTypeAttribute>();
				if(remoteClass==null) continue;

				/*
				var read=type.GetMethod("Read",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(DataInput),typeof(List<object>)},null);
				read??=type.GetMethod("Read",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(DataInput)},null);*/
				var readConstructor=type.GetConstructor([typeof(DataInput),typeof(Func<object,object>)]);
				Func<DataInput,Func<object,object>,object> read;
				if(readConstructor==null){
					readConstructor=type.GetConstructor([typeof(DataInput)]);
					if(readConstructor==null) throw new Exception("Type "+type+" does not implement a public constructor(DataInput i,Func<object,object> already) or public constructor(DataInput i)");

					read=(i,_)=>readConstructor.Invoke([i]);
				} else read=(i,already)=>readConstructor.Invoke([i,already]);


				var writeMethod=type.GetMethod("Write",BindingFlags.Public|BindingFlags.Instance,null,[typeof(DataOutput),typeof(Dictionary<object,int>)],null);
				Action<DataOutput,object,Dictionary<object,int>> write;
				if(writeMethod==null){
					writeMethod=type.GetMethod("Write",BindingFlags.Public|BindingFlags.Instance,null,[typeof(DataOutput)],null);
					if(writeMethod==null) throw new Exception("Type "+type+" does not implement a public void Write(DataOutput o,Dictionary<object,int> already) or public void Write(DataOutput o) method");

					write=(o,d,_)=>writeMethod.Invoke(d,[o]);
				} else write=(o,d,already)=>writeMethod.Invoke(d,[o,already]);


				ReadRegistry.Add(remoteClass.Id,read);
				WriteRegistry.Add((remoteClass.Id,type.IsInstanceOfType,write));
			}
		} catch(Exception e){
			Rpc.Logger.Critical("Error registering assembly \""+assembly+"\": "+e);
		}
	}

	public static void Register(string id,Func<DataInput,Func<object,object>,object> read,Predicate<object> check,Action<DataOutput,object,Dictionary<object,int>> write){
		ReadRegistry.Add(id,read);
		WriteRegistry.Add((id,check,write));
	}

	public static void Register(string id,Predicate<object> check,Action<DataOutput,object,Dictionary<object,int>> write)=>WriteRegistry.Add((id,check,write));

	public static void Register<T>(string id,Func<DataInput,Func<object,object>,T> read,Action<DataOutput,T,Dictionary<object,int>> write)
		=>Register(
			id,
			(data,already)=>read(data,already)!,
			o=>o is T,
			(data,o,already)=>write(data,(T)o,already)
		);

	// Converters are called before a value is being written to the DataOutput, to allow casting from anything to some supported type
	public static void AddConverter(Func<object?,object?> func)=>Converters.Add(func);

	internal static void Free(List<object> already)=>already.OfType<Delegate>().ForEach(RpcFunction.UnregisterFunction);
	internal static bool NeedsFreeing(object arg)=>arg is Delegate;
	internal static void CleanupBeforeFreeing(List<object> already)=>already.RemoveAll(o=>!NeedsFreeing(o));
}