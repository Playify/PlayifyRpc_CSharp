using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;
using static PlayifyRpc.Internal.Data.RpcDataPrimitive;
using Array=System.Array;

namespace PlayifyRpc.Internal.Data;

[RpcSetup]
internal static class RpcDataDefaults{


	static RpcDataDefaults(){
		RegisterFallback(ArraysAndTuples.From,ArraysAndTuples.To,ArraysAndTuples.Stringify);
		RegisterFallback(Enums.From,Enums.To,Enums.Stringify);
		RegisterFallback(ObjectTemplates.From,ObjectTemplates.To,ObjectTemplates.Stringify);//TOOD they should be replaced with direct data types using an Attribute

		RegisterPrimitives();
		Register<RpcDataPrimitive>((p,_)=>p,p=>p,(typescript,_)=>typescript?"any":"dynamic");
		RegisterJson();
		RegisterRpcTypes();
	}

	private static void RegisterPrimitives(){
		Register<VoidType>(
			(_,_)=>new RpcDataPrimitive(),
			_=>default(VoidType),
			(_,_)=>"null");
		Register<DBNull>(
			(_,_)=>new RpcDataPrimitive(),
			p=>p.IsNull()?DBNull.Value:ContinueWithNext,
			(_,_)=>"null");

		Register<bool>(
			(b,_)=>new RpcDataPrimitive(b),
			p=>p.IsBool(out var b)?b:ContinueWithNext,
			(typescript,_)=>typescript?"boolean":"bool");
		Register<string>(
			(s,_)=>new RpcDataPrimitive(s),
			p=>p.IsString(out var s)?s:ContinueWithNext,
			(_,_)=>"string");
		Register<char>(
			(c,_)=>new RpcDataPrimitive(c),
			p=>p.IsChar(out var c)?c:ContinueWithNext,
			(typescript,_)=>typescript?"string":"char");

		Register<sbyte>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(sbyte.MinValue,sbyte.MaxValue,out var n)?(sbyte)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"sbyte");
		Register<byte>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(byte.MaxValue,out var n)?(byte)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"byte");
		Register<short>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(short.MinValue,short.MaxValue,out var n)?(short)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"short");
		Register<ushort>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(ushort.MaxValue,out var n)?(ushort)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"ushort");
		Register<int>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(int.MinValue,int.MaxValue,out var n)?(int)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"int");
		Register<uint>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(uint.MaxValue,out var n)?(uint)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"uint");
		Register<long>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(long.MinValue,long.MaxValue,out var n)?n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"long");
		Register<ulong>(
			(n,_)=>new RpcDataPrimitive(n),
			p=>p.IsNumber(ulong.MaxValue,out var n)?n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"ulong");
		Register<float>(
			(f,_)=>new RpcDataPrimitive(f),
			p=>p.IsNumber(out var d)?(float)d:ContinueWithNext,
			(typescript,_)=>typescript?"number":"float");
		Register<double>(
			(d,_)=>new RpcDataPrimitive(d),
			p=>p.IsNumber(out var d)?d:ContinueWithNext,
			(typescript,_)=>typescript?"number":"double");

		Register<object>(
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
				if(p.IsCustom(out object custom)) return custom;
				throw new RpcDataException("Primitive can't be converted, invalid type detected:"+p);
			},
			(typescript,_)=>typescript?"any":"dynamic");
	}

	private static void RegisterJson(){
		Register<JsonNull>(
			(_,_)=>new RpcDataPrimitive(),
			p=>p.IsNull()?JsonNull.Null:ContinueWithNext,
			(_,_)=>"null");
		Register<JsonBool>(
			(b,_)=>new RpcDataPrimitive(b.Value),
			p=>p.IsBool(out var b)?JsonBool.Get(b):ContinueWithNext,
			(typescript,_)=>typescript?"boolean":"bool");
		Register<JsonNumber>(
			(n,_)=>new RpcDataPrimitive(n.Value),
			p=>p.IsNumber(out var d)?new JsonNumber(d):ContinueWithNext,
			(typescript,_)=>typescript?"number":"double");
		Register<JsonString>(
			(s,_)=>new RpcDataPrimitive(s.Value),
			p=>p.IsString(out var s)?new JsonString(s):ContinueWithNext,
			(_,_)=>"string");
		Register<JsonArray>(
			(a,already)=>already[a]=new RpcDataPrimitive(()=>(a.Select(j=>From(j,already)),a.Count)),
			(p,throwOnError)=>{
				if(p.IsAlready(out JsonArray already)) return already;
				if(!p.IsArray(out var primitives)) return ContinueWithNext;
				return ReadJsonArray(p,primitives,throwOnError)??ContinueWithNext;
			},
			(typescript,_)=>typescript?"any[]":"dynamic[]");
		RegisterObject<JsonObject>(
			e=>e.Select(kv=>(kv.Key,(object?)kv.Value)),
			(e,props,throwOnError)=>props.All(t=>{
				if(ReadJson(t.value,throwOnError) is not{} child) return false;
				e[t.key]=child;
				return true;
			}),
			(_,_)=>"object");
		Register<Json>(
			null,
			(p,throwOnError)=>ReadJson(p,throwOnError)??ContinueWithNext,
			(typescript,_)=>typescript?"any":"dynamic");
		return;

		static Json? ReadJson(RpcDataPrimitive primitive,bool throwOnError){
			if(primitive.IsNull()) return JsonNull.Null;
			if(primitive.IsBool(out var b)) return JsonBool.Get(b);
			if(primitive.IsNumber(out var d)) return new JsonNumber(d);
			if(primitive.IsString(out var s)) return new JsonString(s);
			if(primitive.IsAlready(out Json already)) return already;
			if(primitive.IsArray(out var arr)) return ReadJsonArray(primitive,arr,throwOnError);
			if(primitive.IsObject(out var obj)) return ReadJsonObject(primitive,obj,throwOnError);
			if(throwOnError) throw new InvalidCastException("Error converting primitive "+primitive+" to a Json value");
			return null;
		}

		static JsonArray? ReadJsonArray(RpcDataPrimitive p,IEnumerable<RpcDataPrimitive> primitives,bool throwOnError){
			var array=p.AddAlready(new JsonArray());
			foreach(var child in primitives)
				if(ReadJson(child,throwOnError) is{} json) array.Add(json);
				else return p.RemoveAlready<JsonArray>(array);
			return array;
		}

		static JsonObject? ReadJsonObject(RpcDataPrimitive p,IEnumerable<(string key,RpcDataPrimitive value)> primitives,bool throwOnError){
			var obj=p.AddAlready(new JsonObject());
			foreach(var (key,child) in primitives)
				if(ReadJson(child,throwOnError) is{} json) obj.Add(key,json);
				else return p.RemoveAlready<JsonObject>(obj);
			return obj;
		}
	}

	private static void RegisterRpcTypes(){
		RegisterCustom<DateTime>('D',
			(input,create)=>create(DateTimeOffset.FromUnixTimeMilliseconds(input.ReadLong()).LocalDateTime,false),
			(output,value,_)=>output.WriteLong(new DateTimeOffset(value).ToUnixTimeMilliseconds()),
			(typescript,_)=>typescript?"Date":"DateTime");
		RegisterCustom<byte[]>('b',
			(input,create)=>create(input.ReadFully(input.ReadLength()),true),
			(output,value,_)=>{
				output.WriteLength(value.Length);
				output.Write(value);
			},
			(typescript,_)=>typescript?"Uint8Array":"byte[]");
		RegisterCustom<Exception>('E',
			(input,create)=>create(RpcException.Read(input),true),
			(output,value,_)=>RpcException.WrapAndFreeze(value).Write(output),
			(typescript,_)=>typescript?"RpcError":nameof(RpcException));
		RegisterCustom<Regex>('R',
			(input,create)=>create(new Regex(input.ReadString()??"",(RegexOptions)(input.ReadByte()&3)),true),
			(output,value,_)=>{
				output.WriteString(value.ToString());
				output.WriteByte((byte)(value.Options&(RegexOptions)3));
			},
			(typescript,_)=>typescript?"RegExp":nameof(Regex));
		RegisterCustom<RpcObject>('O',
			(input,create)=>create(new RpcObject(input.ReadString()??throw new NullReferenceException()),true),
			(output,value,_)=>output.WriteString(value.Type),
			(_,_)=>nameof(RpcObject));
		RegisterCustom<RpcFunction>('F',
			(input,create)=>create(new RpcFunction(input.ReadString()??throw new NullReferenceException(),input.ReadString()??throw new NullReferenceException()),true),
			(output,value,_)=>{
				output.WriteString(value.Type);
				output.WriteString(value.Method);
			},
			(_,_)=>nameof(RpcFunction),null,out var writer);
		RegisterFallback(Delegates.From(writer),Delegates.To,Delegates.Stringify);

		RegisterObject<ExpandoObject>(
			e=>e.ToTuples(),
			(e,props,throwOnError)=>props.All(t=>{
				if(!t.value.TryTo(out object obj,throwOnError)) return false;
				((IDictionary<string,object?>)e)[t.key]=obj;
				return true;
			}),
			(_,_)=>"object");
	}


	public static object? ToNullable(RpcDataPrimitive primitive,Type type,bool throwOnError){
		if(Nullable.GetUnderlyingType(type) is not{} nullableType) return ContinueWithNext;
		if(primitive.IsNull()) return null;
		return primitive.TryTo(nullableType,out var result,throwOnError)?result:ContinueWithNext;
	}

	private static class ArraysAndTuples{

		private static readonly Type[] ValueTupleTypes=[
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

		public static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already)=>value switch{
			ITuple t=>already[t]=new RpcDataPrimitive(()=>(Enumerable.Range(0,t.Length).Select(i=>RpcDataPrimitive.From(t[i],already)),t.Length)),
			Array arr=>already[arr]=new RpcDataPrimitive(()=>(arr.Cast<object>().Select(o=>RpcDataPrimitive.From(o,already)),arr.Length)),
			_=>null,
		};

		public static object? To(RpcDataPrimitive primitive,Type type,bool throwOnError){
			if(type.IsArray){
				if(primitive.IsNull()) return null;
				if(primitive.IsAlready(type,out var already)) return already;
				if(!primitive.IsArray(out var arr,out var len)) return ContinueWithNext;

				var elementType=type.GetElementType()!;
				var array=primitive.AddAlready(Array.CreateInstance(elementType,len));
				var i=0;
				foreach(var sub in arr)
					if(sub.TryTo(elementType,out var child,throwOnError)) array.SetValue(child,i++);
					else return primitive.RemoveAlready(array);
				return array;
			}
			if(type.IsGenericType&&ValueTupleTypes.Contains(type.GetGenericTypeDefinition())){
				if(!primitive.IsArray(out var arr,out var len)) return ContinueWithNext;

				var argsTypes=type.GetGenericArguments();
				if(argsTypes.Length!=len) return ContinueWithNext;

				var args=new object?[argsTypes.Length];
				var i=0;
				foreach(var sub in arr)
					if(sub.TryTo(argsTypes[i],out var child,throwOnError)) args[i++]=child;
					else return ContinueWithNext;
				return type.GetConstructor(argsTypes)!.Invoke(args);
			}
			return ContinueWithNext;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tupleName,NullabilityInfo? nullability,string[] generics){
			if(type.IsArray) return RpcDataTypeStringifier.Stringify(type.GetElementType()!,typescript,input,tupleName,nullability?.ElementType)+"[]";
			if(type.IsGenericType&&ValueTupleTypes.Contains(type.GetGenericTypeDefinition())){
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
			return convertible.GetTypeCode()==TypeCode.UInt64
				       ?new RpcDataPrimitive(convertible.ToUInt64(null))
				       :new RpcDataPrimitive(convertible.ToInt64(null));
		}

		public static object? To(RpcDataPrimitive primitive,Type type,bool throwOnError){
			if(!type.IsEnum) return ContinueWithNext;

			if(primitive.IsString(out var s)) return StringEnum.TryParseEnum(type,s,out var result)?result:ContinueWithNext;
			if(primitive.TryTo(type.GetEnumUnderlyingType(),out var number,throwOnError)) return Enum.ToObject(type,number!);
			return ContinueWithNext;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
			if(type.IsEnum) return RpcDataTypeStringifier.TypeName(type,generics);
			return null;
		}
	}

	private static class ObjectTemplates{
		public static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already){
			if(value is not ObjectTemplateBase template) return null;
			return already[template]=new RpcDataPrimitive(()=>template.GetProperties().Select(t=>(t.key,RpcDataPrimitive.From(t.value,already))));
		}

		public static object? To(RpcDataPrimitive primitive,Type type,bool throwOnError){
			if(!typeof(ObjectTemplateBase).IsAssignableFrom(type)) return ContinueWithNext;
			if(primitive.IsNull()) return null;
			if(primitive.IsAlready(type,out var already)) return already;
			if(!primitive.IsObject(out var entries)) return ContinueWithNext;

			var o=(ObjectTemplateBase)Activator.CreateInstance(type)!;
			foreach(var (k,v) in entries)
				if(!o.TrySetProperty(k,v,throwOnError))
					return ContinueWithNext;
			return o;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
			if(type.IsEnum) return RpcDataTypeStringifier.TypeName(type,generics);
			return null;
		}
	}

	private static class Delegates{

		public static FromFuncMaybe From(WriteFunc writer)=>(value,already)=>{
			if(value is not Delegate func) return null;
			var rpcFunction=RpcFunction.RegisterFunction(func);
			return already[func]=new RpcDataPrimitive(rpcFunction,writer,()=>RpcFunction.UnregisterFunction(func));
		};

		public static object? To(RpcDataPrimitive primitive,Type type,bool throwOnError){
			if(!typeof(ObjectTemplateBase).IsAssignableFrom(type)) return ContinueWithNext;
			if(primitive.IsNull()) return null;
			if(primitive.IsAlready(type,out var already)) return already;
			if(!primitive.IsObject(out var entries)) return ContinueWithNext;

			var o=(ObjectTemplateBase)Activator.CreateInstance(type)!;
			foreach(var (k,v) in entries)
				if(!o.TrySetProperty(k,v,throwOnError))
					return ContinueWithNext;
			return o;
		}

		public static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
			if(type.IsEnum) return RpcDataTypeStringifier.TypeName(type,generics);
			return null;
		}
	}

}