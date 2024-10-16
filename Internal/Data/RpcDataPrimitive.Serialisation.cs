using System.Text;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{
	public delegate RpcDataPrimitive ReadFunc(DataInput data,Dictionary<int,RpcDataPrimitive> already,int index);

	public delegate RpcDataPrimitive ReadFunc<out T>(DataInput data,ReadCustomCreator<T> create);

	public delegate RpcDataPrimitive ReadCustomCreator<in T>(T value,bool addAlready);

	public delegate void WriteFunc(DataOutputBuff data,object value,Dictionary<RpcDataPrimitive,int> already);

	public delegate void WriteFunc<in T>(DataOutput data,T value,Dictionary<RpcDataPrimitive,int> already);

	private static readonly Dictionary<char,ReadFunc> ReadByChar=new();
	private static readonly Dictionary<string,ReadFunc> ReadByString=new();

	public void Write(DataOutputBuff output,Dictionary<RpcDataPrimitive,int> already){
		if(IsNull()){
			output.WriteLength('n');
		} else if(IsBool(out var b)){
			output.WriteLength(b?'t':'f');
		} else if(IsNumber(int.MinValue,int.MaxValue,out var i)){
			output.WriteLength('i');
			output.WriteInt((int)i);
		} else if(IsNumber(long.MinValue,long.MaxValue,out var l)){
			output.WriteLength('l');
			output.WriteLong(l);
		} else if(IsNumber(out var d)){
			output.WriteLength('d');
			output.WriteDouble(d);
		} else if(already.TryGetValue(this,out var index)){
			output.WriteLength(-((output.Length-index)*4+0));
		} else{
			already[this]=output.Length;

			if(IsString(out var s)){
				var bytes=Encoding.UTF8.GetBytes(s);
				output.WriteLength(-(bytes.Length*4+1));
				output.Write(bytes);
			} else if(IsArray(out var childs,out var length)){
				output.WriteLength(-(length*4+3));
				foreach(var child in childs) child.Write(output,already);
			} else if(IsObject(out var entries)){
#if NETFRAMEWORK
				if(entries is ICollection<(string key,RpcDataPrimitive value)> collection) length=collection.Count;
				else{
#else
				if(!entries.TryGetNonEnumeratedCount(out length)){
#endif
					var list=entries.ToList();
					length=list.Count;
					entries=list;
				}
				output.WriteLength(-(length*4+2));
				foreach(var (key,value) in entries){
					output.WriteString(key);
					value.Write(output,already);
				}
			} else if(IsCustom(out object custom,out var write)){
				write(output,custom,already);
			} else throw new RpcDataException("Primitive can't be written: "+this);
		}
	}

	public static RpcDataPrimitive[] ReadArray(DataInputBuff input)=>input.ReadArray(already=>Read(input,already),new Dictionary<int,RpcDataPrimitive>())??[];

	public static RpcDataPrimitive Read(DataInputBuff input,Dictionary<int,RpcDataPrimitive> already){
		var index=input.GetBufferOffsetAndLength().off;

		var objectId=input.ReadLength();
		if(objectId<0){
			//Already, String, Object, Array
			objectId=-objectId;
			switch(objectId&3){
				case 0:return already[index-objectId/4];
				case 1:return already[index]=new RpcDataPrimitive(Encoding.UTF8.GetString(input.ReadFully(objectId/4)));
				case 2:{
					var properties=new List<(string key,RpcDataPrimitive value)>();
					var obj=already[index]=new RpcDataPrimitive(()=>properties);
					for(var i=0;i<objectId/4;i++)
						properties.Add((input.ReadString()!,Read(input,already)));
					return obj;
				}
				case 3:{
					var properties=new List<RpcDataPrimitive>();
					var obj=already[index]=new RpcDataPrimitive(()=>(properties,properties.Count));
					for(var i=0;i<objectId/4;i++)
						properties.Add(Read(input,already));
					return obj;
				}
				default:
					throw new RpcDataException("Unreachable code reached");
			}
		}
		if(objectId>=128){
			var type=Encoding.UTF8.GetString(input.ReadFully(objectId-128));
			if(ReadByString.TryGetValue(type,out var read))
				return read(input,already,index);
			throw new RpcDataException($"Invalid data received: objectId=\"{type}\"");
		} else{
			if(ReadByChar.TryGetValue((char)objectId,out var read))
				return read(input,already,index);
			throw new RpcDataException($"Invalid data received: objectId='{(objectId is <32 or 127?$"\\x{objectId:xx}":(char)objectId)}'");
		}
	}
}