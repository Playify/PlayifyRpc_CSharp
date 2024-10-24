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
		var obj=(IRpcDataObject)p.AddAlready(Activator.CreateInstance(type));
		try{
			return !obj.TrySetProps(props,throwOnError)?p.RemoveAlready(obj):obj;
		} catch(Exception) when(FunctionUtils.RunThenReturn(()=>p.RemoveAlready(obj),false)){
			return RpcDataPrimitive.ContinueWithNext;
		}
	}

	private static string? Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
		if(!typeof(IRpcDataObject).IsAssignableFrom(type)) return null;
		if(type.IsInterface) return null;

		return RpcDataTypeStringifier.TypeName(type,generics);
	}


	private static readonly Dictionary<Type,(
		List<(string,Func<object,object>)> getters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> setters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> settersIgnoreCase
		)> Types=new();

	private static (
		List<(string,Func<object,object>)> getters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> setters,
		Dictionary<string,(Type type,Action<object,object?> setValue)> settersIgnoreCase
		) GetTypeInfos(Type type){
		lock(Types)
			if(Types.TryGetValue(type,out var already))
				return already;

		var setters=new Dictionary<string,(Type type,Action<object,object?> setValue)>();
		var settersIgnoreCase=new Dictionary<string,(Type type,Action<object,object?> setValue)>(StringComparer.OrdinalIgnoreCase);
		var getters=new List<(string,Func<object,object>)>();

		foreach(var member in type.GetMembers(BindingFlags.Instance|BindingFlags.Public)){
			if(member is PropertyInfo{IsSpecialName: false} property&&property.GetCustomAttribute<RpcHiddenAttribute>()==null){
				if(property.CanWrite)
					settersIgnoreCase.TryAdd(property.Name,setters[property.Name]=(property.PropertyType,(o,v)=>property.SetValue(o,v)));
				if(property.CanRead)
					getters.Add((property.Name,o=>property.GetValue(o)));
			} else if(member is FieldInfo{IsSpecialName: false} field&&field.GetCustomAttribute<RpcHiddenAttribute>()==null){
				settersIgnoreCase.TryAdd(field.Name,setters[field.Name]=(field.FieldType,(o,v)=>field.SetValue(o,v)));
				getters.Add((field.Name,o=>field.GetValue(o)));
			}
		}

		lock(Types)
			return Types[type]=(getters,setters,settersIgnoreCase);
	}
}