using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[RpcSetup]
internal static class RpcDataDefaults{


	static RpcDataDefaults(){
		RpcDataPrimitive.RegisterGeneric(ArraysAndTuples.From,ArraysAndTuples.To,ArraysAndTuples.Stringify);
		RpcDataPrimitive.RegisterGeneric(Enums.From,Enums.To,Enums.Stringify);
		RpcDataPrimitive.RegisterGeneric(ObjectTemplates.From,ObjectTemplates.To,ObjectTemplates.Stringify);//TOOD they should be replaced with direct data types using an Attribute

		RegisterPrimitives();
		RpcDataPrimitive.Register<RpcDataPrimitive>((p,_)=>p,p=>p,(typescript,_)=>typescript?"any":"dynamic");
		RegisterJson();
		RegisterRpcTypes();
	}

	private static void RegisterPrimitives(){
		RpcDataPrimitive.Register<VoidType>(
			(_,_)=>RpcDataPrimitive.Null,
			p=>p.IsNull()?default(VoidType):RpcDataPrimitive.ContinueWithNext,
			(_,_)=>"null");
		RpcDataPrimitive.Register<DBNull>(
			(_,_)=>RpcDataPrimitive.Null,
			p=>p.IsNull()?DBNull.Value:RpcDataPrimitive.ContinueWithNext,
			(_,_)=>"null");

		RpcDataPrimitive.Register<bool>(
			(b,_)=>b?RpcDataPrimitive.True:RpcDataPrimitive.False,
			p=>p.IsBool(out var b)?b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"boolean":"bool");
		RpcDataPrimitive.Register<string>(
			(s,_)=>RpcDataPrimitive.String(s),
			p=>p.IsString(out var b)?b:RpcDataPrimitive.ContinueWithNext,
			(_,_)=>"string");
		RpcDataPrimitive.Register<char>(
			(s,_)=>RpcDataPrimitive.String(s),
			p=>p.IsChar(out var b)?b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"string":"char");

		RpcDataPrimitive.Register<sbyte>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(sbyte.MinValue,sbyte.MaxValue,out var b)?(sbyte)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"sbyte");
		RpcDataPrimitive.Register<byte>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(byte.MaxValue,out var b)?(byte)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"byte");
		RpcDataPrimitive.Register<short>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(short.MinValue,short.MaxValue,out var b)?(short)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"short");
		RpcDataPrimitive.Register<ushort>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(ushort.MaxValue,out var b)?(ushort)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"ushort");
		RpcDataPrimitive.Register<int>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(int.MinValue,int.MaxValue,out var b)?(int)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"int");
		RpcDataPrimitive.Register<uint>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(uint.MaxValue,out var b)?(uint)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"uint");
		RpcDataPrimitive.Register<long>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(long.MinValue,long.MaxValue,out var b)?b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"long");
		RpcDataPrimitive.Register<ulong>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(ulong.MaxValue,out var b)?b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"ulong");
		RpcDataPrimitive.Register<float>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(out var b)?(float)b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"float");
		RpcDataPrimitive.Register<double>(
			(n,_)=>RpcDataPrimitive.Number(n),
			p=>p.IsNumber(out var b)?b:RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"double");

		RpcDataPrimitive.Register<object>(
			null,
			p=>{
				if(p.IsNull()) return null;
				if(p.IsBool(out var b)) return b;
				if(p.IsNumber(int.MinValue,int.MaxValue,out var i)) return (int)i;
				//if(p.IsNumber(long.MinValue,long.MaxValue,out var l)) return l;//TODO decide what to do with longs
				//if(p.IsNumber(ulong.MaxValue,out var ul)) return ul;
				if(p.IsNumber(out var d)) return d;
				if(p.IsString(out var s)) return s;
				if(p.IsAlready(out object?[] alreadyArr)) return alreadyArr;
				if(p.IsAlready(out ExpandoObject alreadyObj)) return alreadyObj;
				if(p.IsArray(out var arr,out var len)){
					var array=p.AddAlready(new object?[len]);
					i=0;
					foreach(var primitive in arr) array[i++]=primitive.To<object?>();
					return array;
				}
				if(p.IsObject(out var obj)){
					var expando=(IDictionary<string,object?>)p.AddAlready(new ExpandoObject());
					foreach(var (key,primitive) in obj) expando.Add(key,primitive.To<object?>());
					return expando;
				}
				if(p.IsCustom(out object custom))
					return custom;

				throw new RpcDataException("Primitive can't be converted, invalid type detected:"+p,null);
			},
			(typescript,_)=>typescript?"any":"dynamic");
	}

	private static void RegisterJson(){
		RpcDataPrimitive.Register<JsonNull>(
			(_,_)=>RpcDataPrimitive.Null,
			p=>p.IsNull()?JsonNull.Null:RpcDataPrimitive.ContinueWithNext,
			(_,_)=>"null");
		RpcDataPrimitive.Register<JsonBool>(
			(b,_)=>b.Value?RpcDataPrimitive.True:RpcDataPrimitive.False,
			p=>p.IsBool(out var b)?JsonBool.Get(b):RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"boolean":"bool");
		RpcDataPrimitive.Register<JsonNumber>(
			(n,_)=>RpcDataPrimitive.Number(n.Value),
			p=>p.IsNumber(out var d)?new JsonNumber(d):RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"number":"double");
		RpcDataPrimitive.Register<JsonString>(
			(s,_)=>RpcDataPrimitive.String(s),
			p=>p.IsString(out var s)?new JsonString(s):RpcDataPrimitive.ContinueWithNext,
			(_,_)=>"string");
		RpcDataPrimitive.Register<JsonArray>(
			(a,already)=>already[a]=RpcDataPrimitive.Array(()=>(a.Select(j=>RpcDataPrimitive.From(j,already)),a.Count)),
			p=>{
				if(p.IsAlready(out JsonArray already)) return already;
				if(!p.IsArray(out var primitives)) return RpcDataPrimitive.ContinueWithNext;
				return ReadJsonArray(p,primitives)??RpcDataPrimitive.ContinueWithNext;
			},
			(typescript,_)=>typescript?"any[]":"dynamic[]");
		RpcDataPrimitive.RegisterObject<JsonObject>(e=>e.Select(kv=>(kv.Key,(object?)kv.Value)),(e,props)=>props.All(t=>{
				if(!t.value.TryTo(out Json obj)) return false;
				e[t.key]=obj;
				return true;
			}),
			(_,_)=>"object",
			true);
		RpcDataPrimitive.Register<Json>(
			null,
			p=>ReadJson(p)??RpcDataPrimitive.ContinueWithNext,
			(typescript,_)=>typescript?"any":"dynamic");
		return;

		static Json? ReadJson(RpcDataPrimitive primitive){
			if(primitive.IsNull()) return JsonNull.Null;
			if(primitive.IsBool(out var b)) return JsonBool.Get(b);
			if(primitive.IsNumber(out var d)) return new JsonNumber(d);
			if(primitive.IsString(out var s)) return new JsonString(s);
			if(primitive.IsAlready(out Json already)) return already;
			if(primitive.IsArray(out var arr)) return ReadJsonArray(primitive,arr);
			if(primitive.IsObject(out var obj)) return ReadJsonObject(primitive,obj);
			return null;
		}

		static JsonArray? ReadJsonArray(RpcDataPrimitive p,IEnumerable<RpcDataPrimitive> primitives){
			var array=p.AddAlready(new JsonArray());
			foreach(var child in primitives)
				if(ReadJson(child) is{} json) array.Add(json);
				else return p.RemoveAlready<JsonArray>(array);
			return array;
		}

		static JsonObject? ReadJsonObject(RpcDataPrimitive p,IEnumerable<(string key,RpcDataPrimitive value)> primitives){
			var obj=p.AddAlready(new JsonObject());
			foreach(var (key,child) in primitives)
				if(ReadJson(child) is{} json) obj.Add(key,json);
				else return p.RemoveAlready<JsonObject>(obj);
			return obj;
		}
	}

	private static void RegisterRpcTypes(){
		RpcDataPrimitive.RegisterCustom<DateTime>('D',
			(input,_)=>DateTimeOffset.FromUnixTimeMilliseconds(input.ReadLong()).LocalDateTime,
			(output,value,_)=>output.WriteLong(new DateTimeOffset(value).ToUnixTimeMilliseconds()),
			(typescript,_)=>typescript?"Date":"DateTime");
		RpcDataPrimitive.RegisterCustom<byte[]>('b',
			(input,already)=>already(input.ReadFully(input.ReadLength())),
			(output,value,_)=>{
				output.WriteLength(value.Length);
				output.Write(value);
			},
			(typescript,_)=>typescript?"Uint8Array":"byte[]");
		RpcDataPrimitive.RegisterCustom<Exception>('E',
			(input,already)=>already(RpcException.Read(input)),
			(output,value,_)=>RpcException.WrapAndFreeze(value).Write(output),
			(typescript,_)=>typescript?"RpcError":nameof(RpcException));
		RpcDataPrimitive.RegisterCustom<Regex>('R',
			(input,already)=>already(new Regex(input.ReadString()??"",(RegexOptions)(input.ReadByte()&3))),
			(output,value,_)=>{
				output.WriteString(value.ToString());
				output.WriteByte((byte)(value.Options&(RegexOptions)3));
			},
			(typescript,_)=>typescript?"RegExp":nameof(Regex));
		RpcDataPrimitive.RegisterCustom<RpcObject>('O',
			(input,already)=>already(new RpcObject(input.ReadString()??throw new NullReferenceException())),
			(output,value,_)=>output.WriteString(value.Type),
			(_,_)=>nameof(RpcObject));
		RpcDataPrimitive.RegisterCustom<RpcFunction>('F',
			(input,already)=>already(new RpcFunction(input.ReadString()??throw new NullReferenceException(),input.ReadString()??throw new NullReferenceException())),
			(output,value,_)=>{
				output.WriteString(value.Type);
				output.WriteString(value.Method);
			},
			(_,_)=>nameof(RpcFunction));
		RpcDataPrimitive.RegisterObject<ExpandoObject>(
			e=>e.ToTuples(),
			(e,props)=>props.All(t=>{
				if(!t.value.TryTo(out object obj)) return false;
				((IDictionary<string,object?>)e)[t.key]=obj;
				return true;
			}),
			(_,_)=>"object",
			true);

		//TODO new custom StringMap<>
		//TODO RpcDataTypeAttribute
	}


	public static object? ToNullable(RpcDataPrimitive primitive,Type type){
		if(Nullable.GetUnderlyingType(type) is not{} nullableType) return RpcDataPrimitive.ContinueWithNext;
		if(primitive.IsNull()) return null;
		return primitive.TryTo(nullableType,out var result)?result:RpcDataPrimitive.ContinueWithNext;
	}

	private static class ArraysAndTuples{
		public static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already)=>value switch{
			ITuple t=>already[t]=RpcDataPrimitive.Array(()=>(Enumerable.Range(0,t.Length).Select(i=>RpcDataPrimitive.From(t[i],already)),t.Length)),
			Array arr=>already[arr]=RpcDataPrimitive.Array(()=>(arr.Cast<object>().Select(o=>RpcDataPrimitive.From(o,already)),arr.Length)),
			_=>null,
		};

		public static object? To(RpcDataPrimitive primitive,Type type){
			if(type.IsArray){
				if(primitive.IsNull()) return null;
				if(primitive.IsAlready(type,out var already)) return already;
				if(!primitive.IsArray(out var arr,out var len)) return RpcDataPrimitive.ContinueWithNext;

				var elementType=type.GetElementType()!;
				var array=primitive.AddAlready(Array.CreateInstance(elementType,len));
				var i=0;
				foreach(var sub in arr)
					if(sub.TryTo(elementType,out var child)) array.SetValue(child,i++);
					else return primitive.RemoveAlready(array);
				return array;
			}
			if(type.IsGenericType&&RpcDataPrimitive.ValueTupleTypes.Contains(type.GetGenericTypeDefinition())){
				if(!primitive.IsArray(out var arr,out var len)) return RpcDataPrimitive.ContinueWithNext;

				var argsTypes=type.GetGenericArguments();
				if(argsTypes.Length!=len) return RpcDataPrimitive.ContinueWithNext;

				var args=new object?[argsTypes.Length];
				var i=0;
				foreach(var sub in arr)
					if(sub.TryTo(argsTypes[i],out var child)) args[i++]=child;
					else return RpcDataPrimitive.ContinueWithNext;
				return type.GetConstructor(argsTypes)!.Invoke(args);
			}
			return RpcDataPrimitive.ContinueWithNext;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tupleName,NullabilityInfo? nullability,string[] generics){
			if(type.IsArray) return RpcDataTypeStringifier.Stringify(type.GetElementType()!,typescript,input,tupleName,nullability?.ElementType)+"[]";
			if(type.IsGenericType&&RpcDataPrimitive.ValueTupleTypes.Contains(type.GetGenericTypeDefinition())){
				var inner=generics.Select(t=>RpcDataTypeStringifier.Parameter(typescript,t,tupleName())).Join(",");
				return typescript?$"[{inner}]":$"({inner})";
			}
			return null;
		}
	}

	private static class Enums{
		public static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already){
			var type=value.GetType();
			if(!type.IsEnum) return null;
			var convertible=(IConvertible)value;
			return convertible.GetTypeCode()==TypeCode.UInt64?RpcDataPrimitive.Number(convertible.ToUInt64(null)):RpcDataPrimitive.Number(convertible.ToInt64(null));
		}

		public static object To(RpcDataPrimitive primitive,Type type){
			if(!type.IsEnum) return RpcDataPrimitive.ContinueWithNext;

			if(primitive.IsString(out var s)){
#if NETFRAMEWORK
				try{
					return Enum.Parse(type,s,true);
				} catch(ArgumentException){
					return RpcDataPrimitive.ContinueWithNext;
				} catch(OverflowException){
					return RpcDataPrimitive.ContinueWithNext;
				}
#else
			return Enum.TryParse(type,s,true,out var result)?result!:RpcDataPrimitive.ContinueWithNext;
#endif
			}
			if(primitive.TryTo(type.GetEnumUnderlyingType(),out var number))
				return Enum.ToObject(type,number!);
			return RpcDataPrimitive.ContinueWithNext;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
			if(type.IsEnum) return RpcDataTypeStringifier.TypeName(type,generics);
			return null;
		}
	}

	private static class ObjectTemplates{
		public static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already){
			if(value is not ObjectTemplateBase template) return null;
			return already[template]=RpcDataPrimitive.Object(()=>template.GetProperties().Select(t=>(t.key,RpcDataPrimitive.From(t.value,already))));
		}

		public static object? To(RpcDataPrimitive primitive,Type type){
			if(!typeof(ObjectTemplateBase).IsAssignableFrom(type)) return RpcDataPrimitive.ContinueWithNext;
			if(primitive.IsNull()) return null;
			if(primitive.IsAlready(type,out var already)) return already;
			if(!primitive.IsObject(out var entries)) return RpcDataPrimitive.ContinueWithNext;

			var o=(ObjectTemplateBase)Activator.CreateInstance(type)!;
			foreach(var (k,v) in entries)
				if(!o.TrySetProperty(k,v,false))
					return RpcDataPrimitive.ContinueWithNext;
			return o;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
			if(type.IsEnum) return RpcDataTypeStringifier.TypeName(type,generics);
			return null;
		}
	}

}