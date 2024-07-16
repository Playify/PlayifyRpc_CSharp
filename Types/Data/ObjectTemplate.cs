using System.Dynamic;
using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Jsons;
using PlayifyUtility.Streams.Data;
#if NETFRAMEWORK
using PlayifyUtility.Utils.Extensions;
#endif

namespace PlayifyRpc.Types.Data;

public abstract class ObjectTemplate:DynamicObject{
	//InsertionOrderDictionary doesn't help here, as keys are already scrambled, due to normal properties
	private readonly Dictionary<string,object?> _extraProps=new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string,object?> GetExtraProps()=>_extraProps;


	[PublicAPI]
	public static T? CreateFromExpando<T>(ExpandoObject? exp) where T : ObjectTemplate,new(){
		if(exp==null) return null;

		var o=new T();
		foreach(var (key,value) in exp)
			if(!o.TrySetProperty(key,value,true))
				throw new InvalidCastException("Error setting property \""+key+"\" of "+typeof(T).Name);
		return o;
	}

	[PublicAPI]
	public static T? CreateFromJson<T>(JsonObject? exp) where T : ObjectTemplate,new(){
		if(exp==null) return null;

		var o=new T();
		foreach(var (key,value) in exp)
			if(!o.TrySetProperty(key,value,true))
				throw new InvalidCastException("Error setting property \""+key+"\" of "+typeof(T).Name);
		return o;
	}

	internal static ObjectTemplate? TryCreateTemplate(IEnumerable<(string key,object? value)> properties,Type targetType,bool throwOnError){
		if(!typeof(ObjectTemplate).IsAssignableFrom(targetType)) return null;

		var o=(ObjectTemplate)Activator.CreateInstance(targetType)!;
		foreach(var (key,value) in properties)
			if(!o.TrySetProperty(key,value,throwOnError))
				return null;
		return o;
	}

	internal static JsonObject? TryCreateJson(IEnumerable<(string key,object? value)> properties,bool throwOnError){
		var o=new JsonObject();
		foreach(var (key,value) in properties)
			if(DynamicCaster.TryCast(value,out Json casted,throwOnError))
				o[key]=casted;
			else return null;
		return o;
	}

	internal static ExpandoObject TryCreateExpando(IEnumerable<(string key,object? value)> properties,bool throwOnError){
		var o=new ExpandoObject();
		foreach(var (key,value) in properties)
			((IDictionary<string,object?>)o)[key]=DynamicCaster.TryCast(value,out object? casted,throwOnError)?casted:value;
		return o;
	}

	protected virtual bool TrySetProperty(string key,object? value,bool throwOnError){
		var type=GetType();

		if(type.GetProperty(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase) is{
			   CanWrite: true,
		   } property)
			if(DynamicCaster.TryCast(value,property.PropertyType,out var casted,throwOnError)){
				property.SetValue(this,casted);
				return true;
			} else return false;
		if(type.GetField(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase) is{} field)
			if(DynamicCaster.TryCast(value,field.FieldType,out var casted,throwOnError)){
				field.SetValue(this,casted);
				return true;
			} else return false;

		_extraProps[key]=value;
		return true;
	}

	protected internal virtual IEnumerable<(string key,object? value)> GetProperties(){
		foreach(var property in GetType().GetProperties()){
			if(property.IsSpecialName) continue;
			if(!property.CanRead) continue;

			yield return (property.Name,property.GetValue(this));
		}
		foreach(var field in GetType().GetFields()){
			if(field.IsStatic) continue;
			if(field.IsPrivate) continue;

			yield return (field.Name,field.GetValue(this));
		}
		foreach(var (key,value) in _extraProps)
			yield return (key,value);
	}


	internal void WriteDynamic(DataOutput output,List<object> already){
		already.Add(this);

		var tuples=GetProperties().ToArray();
		output.WriteLength(-(tuples.Length*4+2));

		foreach(var (key,value) in tuples){
			output.WriteString(key);
			output.WriteDynamic(value,already);
		}
	}

	public override bool TryGetMember(GetMemberBinder binder,out object result){
		foreach(var (key,value) in GetProperties())
			if(binder.Name.Equals(key,binder.IgnoreCase?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
				return DynamicCaster.TryCast(value,binder.ReturnType,out result!,true);
		result=null!;
		return false;
	}

	public override bool TrySetMember(SetMemberBinder binder,object? value)=>TrySetProperty(binder.Name,value,true);

	public override IEnumerable<string> GetDynamicMemberNames()=>GetProperties().Select(t=>t.key);
}