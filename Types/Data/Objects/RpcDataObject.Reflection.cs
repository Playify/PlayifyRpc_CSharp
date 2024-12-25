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
			(value,already)=>already[value]=new RpcDataPrimitive(()=>((IRpcDataObject)value).GetProps(already)),
			(p,type,throwOnError)=>{
				if(!typeof(IRpcDataObject).IsAssignableFrom(type)) return RpcData.ContinueWithNext;
				if(type.IsAbstract) return RpcData.ContinueWithNext;

				if(p.IsNull()&&RpcData.CanBeNull(type)) return null;
				if(p.IsAlready(type,out var already)) return already;
				if(!p.IsObject(out var props)) return RpcData.ContinueWithNext;
				var obj=(IRpcDataObject)p.AddAlready(Activator.CreateInstance(type)!);
				try{
					return obj.TrySetProps(props,throwOnError,p)?obj:p.RemoveAlready(obj);
				} catch(Exception) when(FunctionUtils.RunThenReturn(()=>p.RemoveAlready(obj),false)){
					throw;
				}
			},
			(type,_,_,_,_,generics)=>type.IsAbstract?null:RpcTypeStringifier.CombineTypeName(type,generics)
		);
	}

	public class Reflection{
		private static readonly Dictionary<Type,Reflection> Cached=new();
		private readonly List<(string key,Func<object,object?> getValue)> _getters=[];
		private readonly Dictionary<string,(Type type,Action<object,object?> setValue)> _setters=new();
		private readonly Dictionary<string,(Type type,Action<object,object?> setValue)> _settersIgnoreCase=new();

		private Reflection(Type type){
			var gettersLimiter=new HashSet<string>();

			foreach(var member in type.GetMembers(BindingFlags.Instance|BindingFlags.Public))
				if(member is PropertyInfo{IsSpecialName: false} property&&!property.IsDefined(typeof(RpcHiddenAttribute),true)){
					var name=property.GetCustomAttribute<RpcNamedAttribute>()?.Name??property.Name;
					if(property.CanWrite)
						_settersIgnoreCase.TryAdd(name,
							_setters[name]=(property.PropertyType,(o,v)=>property.SetValue(o,v)));
					if(property.CanRead&&gettersLimiter.Add(name))
						_getters.Add((name,o=>property.GetValue(o)));
				} else if(member is FieldInfo{IsSpecialName: false} field&&!field.IsDefined(typeof(RpcHiddenAttribute),true)){
					var name=field.GetCustomAttribute<RpcNamedAttribute>()?.Name??field.Name;
					_settersIgnoreCase.TryAdd(name,
						_setters[name]=(field.FieldType,(o,v)=>field.SetValue(o,v)));
					if(gettersLimiter.Add(name))
						_getters.Add((name,o=>field.GetValue(o)));
				}
		}

		private static Reflection Get(Type type){
			lock(Cached)
				return Cached.TryGetValue(type,out var already)?already:Cached[type]=new Reflection(type);
		}

		public static IEnumerable<(string key,RpcDataPrimitive value)> GetProps(object thiz,RpcDataPrimitive.Already already)
			=>Get(thiz.GetType())._getters.Select(t=>(t.key,RpcDataPrimitive.From(t.getValue(thiz),already)));

		public static IEnumerable<(string key,RpcDataPrimitive value)> GetProps(object thiz,RpcDataPrimitive.Already already,GetExtraPropsFunc extraProps)
			=>GetProps(thiz,already).Concat(extraProps(already));

		[PublicAPI]
		public static bool SetProps<T>(ref T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,RpcDataPrimitive original) where T : struct{
			object boxed=thiz;
			try{
				return SetProps(boxed,props,throwOnError,original);
			} finally{
				thiz=(T)boxed;
			}
		}

		[PublicAPI]
		public static bool SetProps<T>(ref T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,
			RpcDataPrimitive original,
			Func<string,RpcDataPrimitive,bool,bool> extraProp) where T : struct{
			var type=thiz.GetType();
			var typeInfo=Get(type);
			foreach(var (key,primitive) in props)
				try{
					if(typeInfo._setters.TryGetValue(key,out var setter)
					   ||typeInfo._settersIgnoreCase.TryGetValue(key,out setter)){
						if(!primitive.TryTo(setter.type,out var result,throwOnError)) return false;
						object boxed=thiz;//Structs need to be boxed before and unboxed afterward
						setter.setValue(boxed,result);
						thiz=(T)boxed;
					} else if(!extraProp(key,primitive,throwOnError))
						if(throwOnError) throw new KeyNotFoundException();
						else return false;
				} catch(Exception e){
					throw new InvalidCastException("Error converting primitive "+original+" to "+RpcTypeStringifier.FromType(type)+
					                               ", due to property "+JsonString.Escape(key),e);
				}
			return true;
		}

		[PublicAPI]
		public static bool SetProps<T>(T thiz,IEnumerable<(string s,RpcDataPrimitive primitive)> props,bool throwOnError,
			RpcDataPrimitive original,
			Func<string,RpcDataPrimitive,bool,bool>? extraProp=null) where T : class{
			var type=thiz.GetType();
			var typeInfo=Get(type);
			foreach(var (key,primitive) in props)
				try{
					if(typeInfo._setters.TryGetValue(key,out var setter)
					   ||typeInfo._settersIgnoreCase.TryGetValue(key,out setter)){
						if(!primitive.TryTo(setter.type,out var result,throwOnError)) return false;
						setter.setValue(thiz,result);
					} else if(extraProp==null||!extraProp(key,primitive,throwOnError))
						if(throwOnError) throw new KeyNotFoundException();
						else return false;
				} catch(Exception e){
					throw new InvalidCastException("Error converting primitive "+original+" to "+RpcTypeStringifier.FromType(type)+", due to property "+JsonString.Escape(key),e);
				}
			return true;
		}
	}

	public delegate IEnumerable<(string key,RpcDataPrimitive value)> GetExtraPropsFunc(RpcDataPrimitive.Already already);

	public delegate bool SetExtraPropFunc(RpcDataPrimitive.Already already);
}