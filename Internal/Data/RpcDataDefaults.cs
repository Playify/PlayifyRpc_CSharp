using System.Collections;
using System.Collections.Specialized;
using System.Dynamic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;
using static PlayifyRpc.Internal.Data.RpcData;
using static PlayifyRpc.Internal.Data.RpcDataPrimitive;
using Array=System.Array;

namespace PlayifyRpc.Internal.Data;

[RpcSetup]
internal static class RpcDataDefaults{


	static RpcDataDefaults(){
		RegisterPrimitives();
		RegisterArraysAndTuples();
		RegisterEnums();
		Jsons.RegisterJson();
		RegisterRpcTypes();
	}

	private static void RegisterPrimitives(){
		Register<RpcDataPrimitive>(
			(p,_,_)=>p,
			(p,_,_)=>p,
			(typescript,_)=>typescript?"any":"dynamic");
		Register<VoidType>(
			(_,_,_)=>new RpcDataPrimitive(),
			(_,_,_)=>default(VoidType),
			(_,_)=>"void");
		Register(
			typeof(Nullable<>),
			null,//c# does this under the hood already
			(p,type,throwOnError,transformer)=>
				p.IsNull()
					?null
					:p.TryTo(type.GetGenericArguments()[0],out var result,throwOnError,transformer)
						?result
						:ContinueWithNext,
			(ts,generics)=>{
				if(ts&&generics[0]=="any") return "any";
				return generics[0]+(ts?"|null":"?");
			}
			//should already be handled in RpcTypeStringifer
		);


		Register<DBNull>(
			(_,_,_)=>new RpcDataPrimitive(),
			(p,_,_)=>p.IsNull()?DBNull.Value:ContinueWithNext,
			(_,_)=>"null");
		Register<bool>(
			(b,_,_)=>new RpcDataPrimitive(b),
			(p,_,_)=>p.IsBool(out var b)?b:ContinueWithNext,
			(typescript,_)=>typescript?"boolean":"bool");
		Register<string>(
			(s,_,_)=>new RpcDataPrimitive(s),
			(p,_,_)=>p.IsString(out var s)?s:p.IsNull()?null:ContinueWithNext,
			(_,_)=>"string");
		Register<char>(
			(c,_,_)=>new RpcDataPrimitive(char.ToString(c)),
			(p,_,_)=>p.IsString(out var s)&&s.Length==1?s[0]:ContinueWithNext,
			(typescript,_)=>typescript?"string":"char");

		Register<sbyte>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>p.IsNumber(sbyte.MinValue,sbyte.MaxValue,out var n)?(sbyte)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"sbyte");
		Register<byte>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>p.IsNumber(0,byte.MaxValue,out var n)?(byte)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"byte");
		Register<short>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>p.IsNumber(short.MinValue,short.MaxValue,out var n)?(short)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"short");
		Register<ushort>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>p.IsNumber(0,ushort.MaxValue,out var n)?(ushort)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"ushort");
		Register<int>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>p.IsNumber(int.MinValue,int.MaxValue,out var n)?(int)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"int");
		Register<uint>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>p.IsNumber(0,uint.MaxValue,out var n)?(uint)n:ContinueWithNext,
			(typescript,_)=>typescript?"number":"uint");
		Register<long>(
			(n,_,_)=>new RpcDataPrimitive(new BigInteger(n)),
			(p,_,_)=>p.IsNumber(long.MinValue,long.MaxValue,out var n)?n:ContinueWithNext,//isNumber should have better performance, and should work as well
			(typescript,_)=>typescript?"bigint":"long");
		Register<ulong>(
			(n,_,_)=>new RpcDataPrimitive(new BigInteger(n)),
			(p,_,_)=>{
				if(p.IsBigIntegerAndNothingElse(out var n)&&n>=0&&n<=ulong.MaxValue) return (ulong)n;
				if(p.IsNumber(0,long.MaxValue,out var l)) return (ulong)l;
				return ContinueWithNext;
			},
			(typescript,_)=>typescript?"bigint":"ulong");
		Register<BigInteger>(
			(n,_,_)=>new RpcDataPrimitive(n),
			(p,_,_)=>{
				if(p.IsBigIntegerAndNothingElse(out var n)) return n;
				if(p.IsNumber(long.MinValue,long.MaxValue,out var l)) return new BigInteger(l);
				// ReSharper disable once CompareOfFloatsByEqualityOperator
				if(p.IsNumber(out var d)&&Math.Floor(d)==d) return new BigInteger(d);
				return ContinueWithNext;
			},
			(typescript,_)=>typescript?"bigint":"BigInteger");
		Register<float>(
			(f,_,_)=>new RpcDataPrimitive(f),
			(p,_,_)=>p.IsNumber(out var d)?(float)d:ContinueWithNext,
			(typescript,_)=>typescript?"number":"float");
		Register<double>(
			(d,_,_)=>new RpcDataPrimitive(d),
			(p,_,_)=>p.IsNumber(out var d)?d:ContinueWithNext,
			(typescript,_)=>typescript?"number":"double");

		Register<object>(
			null,
			(p,_,transformer)=>{
				if(p.IsNull()) return null;
				if(p.IsBool(out var b)) return b;
				if(p.IsBigIntegerAndNothingElse(out var big)) return big;
				if(p.IsNumber(int.MinValue,int.MaxValue,out var i)) return (int)i;
				if(p.IsNumber(out var d)) return d;
				if(p.IsString(out var s)) return s;
				if(p.IsAlready(out object?[] alreadyArr)) return alreadyArr;
				if(p.IsAlready(out ExpandoObject alreadyObj)) return alreadyObj;
				if(p.IsArray(out var arr,out var len)){
					var array=p.AddAlready(new object?[len]);
					i=0;
					//Maybe this should use TryTo instead of To, but converting to object should never fail in the first place
					foreach(var primitive in arr) array[i++]=primitive.To<object?>(transformer);
					return array;
				}
				if(p.IsObject(out var obj)){
					IDictionary<string,object?> expando=p.AddAlready(new ExpandoObject());
					//Maybe this should use TryTo instead of To, but converting to object should never fail in the first place
					foreach(var (key,primitive) in obj) expando.Add(key,primitive.To<object?>(transformer));
					return expando;
				}
				if(p.IsCustom(out object custom)) return custom;
				throw new RpcDataException("Primitive can't be converted, invalid type detected:"+p);
			},
			(typescript,_)=>typescript?"any":"dynamic");
	}

	private static void RegisterRpcTypes(){
		RegisterCustom<DateTime>('D',
			(input,create)=>create(DateTimeOffset.FromUnixTimeMilliseconds(input.ReadLong()).LocalDateTime),
			(output,value,_)=>output.WriteLong(new DateTimeOffset(value).ToUnixTimeMilliseconds()),
			(typescript,_)=>typescript?"Date":"DateTime",
			date=>date.ToString("s"));
		RegisterCustom<Exception>('E',
			(input,create)=>create(RpcException.Read(input)),
			(output,value,_)=>RpcException.WrapAndFreeze(value).Write(output),
			(typescript,_)=>typescript?"RpcError":nameof(RpcException));
		RegisterCustom<Regex>('R',
			(input,create)=>create(new Regex(input.ReadString()??"",(RegexOptions)(input.ReadByte()&3))),
			(output,value,_)=>{
				output.WriteString(value.ToString());
				output.WriteByte((byte)(value.Options&(RegexOptions)3));
			},
			(typescript,_)=>typescript?"RegExp":nameof(Regex),
			regex=>{
				var s="/"+regex.ToString().Replace("/","\\/")+"/";
				if((regex.Options&RegexOptions.IgnoreCase)!=0) s+="i";
				if((regex.Options&RegexOptions.Multiline)!=0) s+="m";
				return s;
			});
		RegisterCustom<RpcObject>('O',
			(input,create)=>create(new RpcObject(input.ReadString()??throw new NullReferenceException())),
			(output,value,_)=>output.WriteString(value.Type),
			(_,_)=>nameof(RpcObject));
		var rpcFunctionWriter=RegisterCustom<RpcFunction>('F',
			(input,create)=>create(new RpcFunction(
				input.ReadString()??throw new NullReferenceException(),
				input.ReadString()??throw new NullReferenceException())),
			(output,value,_)=>{
				output.WriteString(value.Type);
				output.WriteString(value.Method);
			},
			(_,_)=>nameof(RpcFunction));
		Register<Delegate>(
			(func,already,_)=>{
				already.OnDispose(()=>RpcFunction.UnregisterFunction(func));
				return already[func]=new RpcDataPrimitive(
					       RpcFunction.RegisterFunction(func),rpcFunctionWriter,
					       null);
			},
			null,
			(_,_)=>nameof(RpcFunction)
		);

		Register<ExpandoObject>(
			(exp,already,transformer)=>already[exp]=new RpcDataPrimitive(()=>exp.Select(j=>(j.Key,From(j.Value,already,transformer)))),
			(p,throwOnError,transformer)=>{
				if(p.IsNull()) return null;
				if(p.IsAlready(out ExpandoObject already)) return already;
				if(!p.IsObject(out var props)) return ContinueWithNext;
				IDictionary<string,object?> expando=p.AddAlready(new ExpandoObject());
				foreach(var (key,child) in props)
					try{
						if(child.TryTo(out object? o,throwOnError,transformer)) expando.Add(key,o);
						else return p.RemoveAlready(expando);
					} catch(Exception e){
						p.RemoveAlready(expando);
						throw new InvalidCastException("Error converting primitive "+p+" to ExpandoObject, due to property "+JsonString.Escape(key),e);
					}
				return expando;
			},
			(_,_)=>"object");
		Register<NameValueCollection>(
			(nvc,already,transformer)=>already[nvc]=new RpcDataPrimitive(()=>nvc.Keys.Cast<string>().Select(k=>(k??"",From(nvc.Get(k),already,transformer)))),
			(p,throwOnError,_)=>{
				if(p.IsNull()) return null;
				if(p.IsAlready(out NameValueCollection already)) return already;
				if(!p.IsObject(out var props)) return ContinueWithNext;
				var nvc=p.AddAlready(new NameValueCollection());
				foreach(var (key,child) in props)
					try{
						if(child.IsString(out var s)) nvc.Add(key,s);
						else if(throwOnError) throw new InvalidCastException("Error converting "+child+" to string");
						else return p.RemoveAlready(nvc);
					} catch(Exception e){
						p.RemoveAlready(nvc);
						throw new InvalidCastException("Error converting primitive "+p+" to ExpandoObject, due to property "+JsonString.Escape(key),e);
					}
				return nvc;
			},
			(_,_)=>"object");
	}

	private static void RegisterArraysAndTuples(){
		Register<ValueTuple>(
			(_,_,_)=>new RpcDataPrimitive(()=>([],0)),
			(p,__,___)=>p.IsArray(out _,out var length)&&length==0?new ValueTuple():ContinueWithNext,
			(typescript,_)=>typescript?"[]":"()"
		);
		foreach(var type in (Type[])[
			        //typeof(ValueTuple),
			        typeof(ValueTuple<>),
			        typeof(ValueTuple<,>),
			        typeof(ValueTuple<,,>),
			        typeof(ValueTuple<,,,>),
			        typeof(ValueTuple<,,,,>),
			        typeof(ValueTuple<,,,,,>),
			        typeof(ValueTuple<,,,,,,>),
			        typeof(ValueTuple<,,,,,,,>),
		        ])
			Register(
				type,
				(value,already,transformer)=>{
					//don't set it in already, as it's a value type.
					var t=(ITuple)value;
					return new RpcDataPrimitive(()=>(
						                                Enumerable.Range(0,t.Length).Select(i=>
							                                From(t[i],already,transformer)),t.Length));
				},
				(p,t,throwOnError,transformer)=>{
					if(!p.IsArray(out var arr,out var len)) return ContinueWithNext;

					var argsTypes=t.GetGenericArguments();
					if(argsTypes.Length!=len) return ContinueWithNext;

					var args=new object?[argsTypes.Length];
					var i=0;
					foreach(var sub in arr)
						try{
							if(sub.TryTo(argsTypes[i],out var child,throwOnError,transformer)) args[i++]=child;
							else return ContinueWithNext;
						} catch(Exception e){
							throw new InvalidCastException("Error converting primitive "+p+" to "+RpcTypeStringifier.FromType(t)+", due to index "+i,e);
						}
					return t.GetConstructor(argsTypes)!.Invoke(args);
				},
				(_,typescript,_,tupleName,_,generics)=>{
					var inner=generics.Select(t=>RpcTypeStringifier.CombineParameter(typescript,t,tupleName())).Join(",");
					return typescript?$"[{inner}]":$"({inner})";
				});


		Register(
			typeof(Array),
			(list,already,transformer)=>already[list]=new RpcDataPrimitive(()=>(
				                                                                   ((IEnumerable)list).Cast<object>().Select(o=>From(o,already,transformer))
				                                                                   ,((IList)list).Count)),
			(p,type,throwOnError,transformer)=>{
				if(p.IsNull()) return null;
				if(p.IsAlready(type,out var already)) return already;
				if(!p.IsArray(out var arr,out var len)) return ContinueWithNext;
				if(type==typeof(Array)) type=typeof(object[]);

				var elementType=type.GetElementType()!;
				var array=p.AddAlready(Array.CreateInstance(elementType,len));
				var i=0;
				foreach(var sub in arr)
					try{
						if(sub.TryTo(elementType,out var child,throwOnError,transformer)) array.SetValue(child,i++);
						else return p.RemoveAlready(array);
					} catch(Exception e){
						p.RemoveAlready(array);
						throw new InvalidCastException("Error converting primitive "+p+" to "+RpcTypeStringifier.FromType(elementType)+"[], due to index "+i,e);
					}
				return array;
			},
			(type,typescript,input,tupleName,nullability,_)=>{
				if(type==typeof(Array)) return typescript?"any[]":"dynamic[]";
				return RpcTypeStringifier.StringifySubType(type.GetElementType()!,typescript,input,tupleName,nullability?.ElementType,null)+"[]";
			});

		Register(
			typeof(List<>),
			(list,already,transformer)=>already[list]=new RpcDataPrimitive(()=>(
				                                                                   ((IEnumerable)list).Cast<object>().Select(o=>From(o,already,transformer))
				                                                                   ,((IList)list).Count)),
			(p,type,throwOnError,transformer)=>{
				if(p.IsNull()) return null;
				if(p.IsAlready(type,out var already)) return already;
				if(!p.IsArray(out var arr)) return ContinueWithNext;
				var elementType=type.GetGenericArguments()[0];
				var instance=p.AddAlready((IList)Activator.CreateInstance(type)!);
				foreach(var sub in arr)
					try{
						if(sub.TryTo(elementType,out var child,throwOnError,transformer)) instance.Add(child);
						else return p.RemoveAlready(instance);
					} catch(Exception e){
						p.RemoveAlready(instance);
						throw new InvalidCastException("Error converting primitive "+p+" to List<"+RpcTypeStringifier.FromType(elementType)+">, due to index "+instance.Count,e);
					}
				return instance;
			},
			(_,generics)=>generics.Single()+"[]");


		var writeBytes=RegisterCustom<byte[]>('b',
			(input,create)=>create(input.ReadFully(input.ReadLength())),
			(output,value,_)=>{
				output.WriteLength(value.Length);
				output.Write(value);
			},
			(typescript,_)=>typescript?"Uint8Array":"byte[]",
			bytes=>$"[{bytes.Join(',')}]");
		Register(
			typeof(ArraySegment<>),
			(list,already,transformer)=>already[list]=
				                            list is ArraySegment<byte> bytes
					                            ?new RpcDataPrimitive(bytes.ToArray(),writeBytes,()=>$"[{bytes.Join(',')}]")
					                            :new RpcDataPrimitive(()=>(
						                                                      ((IEnumerable)list).Cast<object>().Select(o=>From(o,already,transformer))
						                                                      ,((IList)list).Count)),
			(p,type,throwOnError,transformer)=>p.TryTo(type.GetGenericArguments()[0].MakeArrayType(),out var array,throwOnError,transformer)
				                                   ?Activator.CreateInstance(type,array)
				                                   :ContinueWithNext,
			(_,generics)=>generics.Single()+"[]");
	}

	private static class Jsons{
		public static void RegisterJson(){
			Register<JsonNull>(
				(_,_,_)=>new RpcDataPrimitive(),
				(p,_,_)=>p.IsNull()?JsonNull.Null:ContinueWithNext,
				(_,_)=>"null");
			Register<JsonBool>(
				(b,_,_)=>new RpcDataPrimitive(b.Value),
				(p,_,_)=>p.IsBool(out var b)?JsonBool.Get(b):ContinueWithNext,
				(typescript,_)=>typescript?"boolean":"bool");
			Register<JsonNumber>(
				(n,_,_)=>new RpcDataPrimitive(n.Value),
				(p,_,_)=>p.IsNumber(out var d)?new JsonNumber(d):ContinueWithNext,
				(typescript,_)=>typescript?"number":"double");
			Register<JsonString>(
				(s,_,_)=>new RpcDataPrimitive(s.Value),
				(p,_,_)=>p.IsString(out var s)?new JsonString(s):ContinueWithNext,
				(_,_)=>"string");
			Register<JsonArray>(
				(a,already,transformer)=>already[a]=new RpcDataPrimitive(()=>(a.Select(j=>From(j,already,transformer)),a.Count)),
				(p,throwOnError,_)=>{
					if(p.IsAlready(out JsonArray already)) return already;
					if(!p.IsArray(out var primitives)) return ContinueWithNext;
					return ReadJsonArray(p,primitives,throwOnError)??ContinueWithNext;
				},
				(typescript,_)=>typescript?"any[]":"dynamic[]");
			Register<JsonObject>(
				(o,already,transformer)=>already[o]=new RpcDataPrimitive(()=>o.Select(j=>(j.Key,From(j.Value,already,transformer)))),
				(p,throwOnError,_)=>{
					if(p.IsAlready(out JsonObject already)) return already;
					if(!p.IsObject(out var props)) return ContinueWithNext;
					return ReadJsonObject(p,props,throwOnError)??ContinueWithNext;
				},
				(_,_)=>"object");
			Register<Json>(
				null,
				(p,throwOnError,_)=>ReadJson(p,throwOnError)??ContinueWithNext,
				(typescript,_)=>typescript?"any":"dynamic");
		}

		private static Json? ReadJson(RpcDataPrimitive primitive,bool throwOnError){
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

		private static JsonArray? ReadJsonArray(RpcDataPrimitive p,IEnumerable<RpcDataPrimitive> primitives,bool throwOnError){
			var array=p.AddAlready(new JsonArray());
			foreach(var child in primitives)
				try{
					if(ReadJson(child,throwOnError) is{} json) array.Add(json);
					else{
						p.RemoveAlready(array);
						return null;
					}
				} catch(Exception e){
					p.RemoveAlready(array);
					throw new InvalidCastException("Error converting primitive "+p+" to JsonArray, due to index "+array.Count,e);
				}
			return array;
		}

		private static JsonObject? ReadJsonObject(RpcDataPrimitive p,IEnumerable<(string key,RpcDataPrimitive value)> primitives,bool throwOnError){
			var obj=p.AddAlready(new JsonObject());
			foreach(var (key,child) in primitives)
				try{
					if(ReadJson(child,throwOnError) is{} json) obj.Add(key,json);
					else{
						p.RemoveAlready(obj);
						return null;
					}
				} catch(Exception e){
					p.RemoveAlready(obj);
					throw new InvalidCastException("Error converting primitive "+p+" to JsonObject, due to property "+JsonString.Escape(key),e);
				}
			return obj;
		}
	}

	private static void RegisterEnums(){
		Register(
			typeof(Enum),
			(value,_,_)=>{
				var convertible=(IConvertible)value;
				return convertible.GetTypeCode() switch{
					TypeCode.Int64=>new RpcDataPrimitive(new BigInteger(convertible.ToInt64(null))),
					TypeCode.UInt64=>new RpcDataPrimitive(new BigInteger(convertible.ToUInt64(null))),
					_=>new RpcDataPrimitive(convertible.ToInt64(null)),
				};
			},
			(p,type,throwOnError,transformer)=>{
				if(type==typeof(Enum)) return ContinueWithNext;
				if(p.IsString(out var s)) return StringEnums.TryParseEnum(type,s,out var result)?result:ContinueWithNext;
				if(p.TryTo(type.GetEnumUnderlyingType(),out var number,throwOnError,transformer)) return Enum.ToObject(type,number!);
				return ContinueWithNext;
			},
			(type,_,_,_,_,generics)=>type==typeof(Enum)?null:RpcTypeStringifier.CombineTypeName(type,generics));
	}
}