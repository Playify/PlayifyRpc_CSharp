using System.Reflection;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils;

namespace PlayifyRpc.Types.Data.Objects;

[RpcSetup]
public partial class RpcDataObject{
	static RpcDataObject(){
		RpcDataPrimitive.RegisterFallback(From,To,Stringify);
	}

	private static RpcDataPrimitive? From(object value,Dictionary<object,RpcDataPrimitive> already)
		=>value is not IRpcDataObject dataObject?null:new RpcDataPrimitive(()=>dataObject.GetProps(already));

	private static object? To(RpcDataPrimitive p,Type type,bool throwOnError){
		if(!typeof(IRpcDataObject).IsAssignableFrom(type)) return RpcDataPrimitive.ContinueWithNext;
		if(type.IsInterface) return RpcDataPrimitive.ContinueWithNext;

		if(p.IsNull()&&RpcDataPrimitive.CanBeNull(type)) return null;
		if(p.IsAlready(type,out var already)) return already;
		if(!p.IsObject(out var props)) return RpcDataPrimitive.ContinueWithNext;
		var obj=(IRpcDataObject)p.AddAlready(Activator.CreateInstance(type)!);
		try{
			return obj.TrySetProps(props,throwOnError)?obj:p.RemoveAlready(obj);
		} catch(Exception) when(FunctionUtils.RunThenReturn(()=>p.RemoveAlready(obj),false)){
			throw;
		}
	}

	private static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
		if(!typeof(IRpcDataObject).IsAssignableFrom(type)) return null;
		if(type.IsInterface) return null;

		return RpcTypeStringifier.TypeName(type,generics);
	}

	private static readonly Dictionary<Type,(
		List<(string,Func<object,object?>)> getters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> setters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> settersIgnoreCase
		)> Types=new();

	private static (
		List<(string,Func<object,object?>)> getters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> setters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> settersIgnoreCase
		) GetTypeInfos(Type type){
		lock(Types)
			if(Types.TryGetValue(type,out var already))
				return already;

		var gettersLimiter=new HashSet<string>();
		var getters=new List<(string,Func<object,object?>)>();
		var setters=new Dictionary<string,(Type type,Action<object,object?> setValue)>();
		var settersIgnoreCase=new Dictionary<string,(Type type,Action<object,object?> setValue)>(StringComparer.OrdinalIgnoreCase);

		foreach(var member in type.GetMembers(BindingFlags.Instance|BindingFlags.Public))
			if(member is PropertyInfo{IsSpecialName: false} property&&!property.IsDefined(typeof(RpcHiddenAttribute),true)){
				var name=property.GetCustomAttribute<RpcNamedAttribute>()?.Name??property.Name;
				if(property.CanWrite)
					settersIgnoreCase.TryAdd(name,
						setters[name]=(property.PropertyType,(o,v)=>property.SetValue(o,v)));
				if(property.CanRead&&gettersLimiter.Add(name))
					getters.Add((name,o=>property.GetValue(o)));
			} else if(member is FieldInfo{IsSpecialName: false} field&&!field.IsDefined(typeof(RpcHiddenAttribute),true)){
				var name=field.GetCustomAttribute<RpcNamedAttribute>()?.Name??field.Name;
				settersIgnoreCase.TryAdd(name,
					setters[name]=(field.FieldType,(o,v)=>field.SetValue(o,v)));
				if(gettersLimiter.Add(name))
					getters.Add((name,o=>field.GetValue(o)));
			}

		lock(Types)
			return Types[type]=(getters,setters,settersIgnoreCase);
	}
}