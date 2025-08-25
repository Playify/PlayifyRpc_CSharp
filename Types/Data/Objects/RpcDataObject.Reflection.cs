using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;

namespace PlayifyRpc.Types.Data.Objects;

[RpcSetup]
public partial class RpcDataObject{
	static RpcDataObject(){
		RpcData.Register(
			typeof(IRpcDataObject),
			(value,already,transformer)=>already[value]=new RpcDataPrimitive(()=>((IRpcDataObject)value).GetProps(already,transformer)),
			(p,type,throwOnError,transformer)=>{
				if(!typeof(IRpcDataObject).IsAssignableFrom(type)) return RpcData.ContinueWithNext;
				if(type.IsAbstract) return RpcData.ContinueWithNext;

				if(p.IsNull()&&RpcData.CanBeNull(type)) return null;
				if(p.IsAlready(type,out var already)) return already;
				if(!p.IsObject(out var props)) return RpcData.ContinueWithNext;
				var obj=(IRpcDataObject)p.AddAlready(Activator.CreateInstance(type)!);
				try{
					return obj.TrySetProps(props,throwOnError,transformer,p)?obj:p.RemoveAlready(obj);
				} catch(Exception) when(FunctionUtils.RunThenReturn(()=>p.RemoveAlready(obj),false)){
					throw;
				}
			},
			(type,_,_,_,_,generics)=>type.IsAbstract?null:RpcTypeStringifier.CombineTypeName(type,generics)
		);
	}

	public class Reflection{
		private static readonly Dictionary<Type,Reflection> Cached=new();
		private static readonly Dictionary<Type,Reflection> CachedStatic=new();
		private readonly List<(string key,Func<object?,RpcDataPrimitive.Already,RpcDataTransformerAttribute?,RpcDataPrimitive> getValue)> _getters=[];

		private delegate bool SetterFunc(object? thiz,RpcDataPrimitive value,bool throwOnError,RpcDataTransformerAttribute? transformer);

		private readonly Dictionary<string,SetterFunc> _setters=new();
		private readonly Dictionary<string,SetterFunc> _settersIgnoreCase=new();

		private Reflection(Type type,bool @static){
			var gettersLimiter=new HashSet<string>();

			foreach(var member in type.GetMembers(@static?BindingFlags.Static|BindingFlags.Public:BindingFlags.Instance|BindingFlags.Public))
				if(member is PropertyInfo{IsSpecialName: false} property&&!property.IsDefined(typeof(RpcHiddenAttribute),true)){
					var name=property.GetCustomAttribute<RpcNamedAttribute>()?.Name??property.Name;
					if(property.CanWrite)
						_settersIgnoreCase.TryAdd(name,
							_setters[name]=Setter(property.PropertyType,(o,v)=>property.SetValue(o,v),
								property.GetCustomAttribute<RpcDataTransformerAttribute>()));
					if(property.CanRead&&gettersLimiter.Add(name))
						_getters.Add((name,Getter(o=>property.GetValue(o),property.GetCustomAttribute<RpcDataTransformerAttribute>())));
				} else if(member is FieldInfo{IsSpecialName: false} field&&!field.IsDefined(typeof(RpcHiddenAttribute),true)){
					var name=field.GetCustomAttribute<RpcNamedAttribute>()?.Name??field.Name;
					_settersIgnoreCase.TryAdd(name,
						_setters[name]=Setter(field.FieldType,(o,v)=>field.SetValue(o,v),
							field.GetCustomAttribute<RpcDataTransformerAttribute>()));
					if(gettersLimiter.Add(name))
						_getters.Add((name,Getter(o=>field.GetValue(o),field.GetCustomAttribute<RpcDataTransformerAttribute>())));
				} else if(@static&&member is Type{IsSpecialName: false} nested&&!nested.IsDefined(typeof(RpcHiddenAttribute),true)){
					var name=nested.GetCustomAttribute<RpcNamedAttribute>()?.Name??nested.Name;
					var definedTransformer=nested.GetCustomAttribute<RpcDataTransformerAttribute>();
					_settersIgnoreCase.TryAdd(name,
						_setters[name]=(_,value,throwOnError,fallbackTransformer)=>StaticLoad(nested,value,throwOnError,definedTransformer??fallbackTransformer));
					if(gettersLimiter.Add(name))
						_getters.Add((name,(_,already,fallbackTransformer)=>StaticSave(nested,already,definedTransformer??fallbackTransformer)));
				}
		}

		private static Func<object?,RpcDataPrimitive.Already,RpcDataTransformerAttribute?,RpcDataPrimitive> Getter(Func<object?,object?> getter,RpcDataTransformerAttribute? transformer)
			=>(thiz,already,fallbackTransformer)=>RpcDataPrimitive.From(getter(thiz),already,transformer??fallbackTransformer);

		private static SetterFunc Setter(Type type,Action<object?,object?> setter,RpcDataTransformerAttribute? transformer)
			=>(thiz,value,throwOnError,fallbackTransformer)=>{
				if(!value.TryTo(type,out var result,throwOnError,transformer??fallbackTransformer)) return false;
				setter(thiz,result);
				return true;
			};

		private static Reflection Get(Type type){
			lock(Cached)
				return Cached.TryGetValue(type,out var already)?already:Cached[type]=new Reflection(type,false);
		}

		private static Reflection GetStatic(Type type){
			lock(CachedStatic)
				return CachedStatic.TryGetValue(type,out var already)?already:CachedStatic[type]=new Reflection(type,true);
		}

		public static IEnumerable<(string key,RpcDataPrimitive value)> GetProps(object thiz,RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer)
			=>thiz is Type type
				  ?GetStatic(type)._getters.Select(t=>(t.key,t.getValue(null,already,transformer)))
				  :Get(thiz.GetType())._getters.Select(t=>(t.key,t.getValue(thiz,already,transformer)));

		public static IEnumerable<(string key,RpcDataPrimitive value)> GetProps(object thiz,RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer,GetExtraPropsFunc extraProps)
			=>GetProps(thiz,already,transformer).Concat(extraProps(already,transformer));

		[PublicAPI]
		public static bool SetProps<T>(ref T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,RpcDataTransformerAttribute? transformer,RpcDataPrimitive original) where T : struct{
			object boxed=thiz;
			try{
				return SetProps(boxed,props,throwOnError,transformer,original);
			} finally{
				thiz=(T)boxed;
			}
		}

		[PublicAPI]
		public static bool SetProps<T>(ref T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,RpcDataTransformerAttribute? transformer,
			RpcDataPrimitive original,Func<string,RpcDataPrimitive,bool,RpcDataTransformerAttribute?,bool> extraProp) where T : struct{
			var type=thiz.GetType();
			var typeInfo=Get(type);
			foreach(var (key,primitive) in props)
				try{
					if(typeInfo._setters.TryGetValue(key,out var setter)
					   ||typeInfo._settersIgnoreCase.TryGetValue(key,out setter)){
						object boxed=thiz;//Structs need to be boxed before and unboxed afterward, so that extraProp function works properly
						if(!setter(boxed,primitive,throwOnError,transformer)) return false;
						thiz=(T)boxed;
					} else if(!extraProp(key,primitive,throwOnError,transformer))
						if(throwOnError) throw new KeyNotFoundException();
						else return false;
				} catch(Exception e){
					throw new InvalidCastException("Error converting primitive "+original+" to "+RpcTypeStringifier.FromType(type)+
					                               ", due to property "+JsonString.Escape(key),e);
				}
			return true;
		}

		[PublicAPI]
		public static bool SetProps<T>(T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,RpcDataTransformerAttribute? transformer,
			RpcDataPrimitive original,Func<string,RpcDataPrimitive,bool,RpcDataTransformerAttribute?,bool>? extraProp=null) where T : class{
			Reflection typeInfo;
			if(thiz is Type type){
				thiz=null!;
				typeInfo=GetStatic(type);
			} else
				typeInfo=Get(thiz.GetType());

			foreach(var (key,primitive) in props)
				try{
					if(typeInfo._setters.TryGetValue(key,out var setter)
					   ||typeInfo._settersIgnoreCase.TryGetValue(key,out setter)){
						if(!setter(thiz,primitive,throwOnError,transformer)) return false;
					} else if(extraProp==null||!extraProp(key,primitive,throwOnError,transformer))
						if(throwOnError) throw new KeyNotFoundException();
						else return false;
				} catch(Exception e){
					throw new InvalidCastException("Error converting primitive "+original+" to "+RpcTypeStringifier.FromType(thiz as Type??thiz.GetType())+", due to property "+JsonString.Escape(key),e);
				}
			return true;
		}
	}

	public delegate IEnumerable<(string key,RpcDataPrimitive value)> GetExtraPropsFunc(RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer);

	public delegate bool SetExtraPropFunc(RpcDataPrimitive.Already already,RpcDataTransformerAttribute? transformer);
}