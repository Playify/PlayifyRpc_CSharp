using System.Numerics;
using System.Text;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{

	internal static readonly Dictionary<char,RpcData.ReadFunc> ReadByChar=new(){
		{'n',(_,_,_)=>new RpcDataPrimitive()},
		{'t',(_,_,_)=>new RpcDataPrimitive(true)},
		{'f',(_,_,_)=>new RpcDataPrimitive(false)},
		{'i',(input,_,_)=>new RpcDataPrimitive(input.ReadInt())},
		{'d',(input,_,_)=>new RpcDataPrimitive(input.ReadDouble())},{
			'+',(input,_,_)=>{
				var length=input.ReadLength();
				var bytes=new byte[length+1];
				input.ReadFully(bytes,0,length);
				return new RpcDataPrimitive(new BigInteger(bytes));
			}
		},{
			'-',(input,_,_)=>{
				var length=input.ReadLength();
				var bytes=new byte[length+1];
				input.ReadFully(bytes,0,length);
				return new RpcDataPrimitive(-new BigInteger(bytes));
			}
		},
	};
	internal static readonly Dictionary<string,RpcData.ReadFunc> ReadByString=new();

	public void Write(DataOutputBuff output,Dictionary<RpcDataPrimitive,int> already){
		if(IsNull()){
			output.WriteLength('n');
		} else if(IsBool(out var b)){
			output.WriteLength(b?'t':'f');
		} else if(IsBigIntegerAndNothingElse(out var big)){
			if(big.Sign<0){
				output.WriteLength('-');
				big=-big;
			} else output.WriteLength('+');
			var bytes=big.ToByteArray();
			var length=bytes[bytes.Length-1]==0?bytes.Length-1:bytes.Length;
			output.WriteLength(length);
			output.Write(bytes,0,length);
		} else if(IsNumber(int.MinValue,int.MaxValue,out var i)){
			output.WriteLength('i');
			output.WriteInt((int)i);
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

	public static RpcDataPrimitive[] ReadArray(DataInputBuff input)
		=>input.ReadArray(already=>Read(input,already),new Dictionary<int,RpcDataPrimitive>())??[];

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