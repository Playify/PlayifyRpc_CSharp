using System.Dynamic;
using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyUtility.Jsons;
using PlayifyUtility.Streams.Data;
#if NETFRAMEWORK
using PlayifyUtility.Utils.Extensions;
#endif

namespace PlayifyRpc.Types.Data;

public abstract class ObjectTemplate{
	private readonly Dictionary<string,object?> _extraProps=new(StringComparer.OrdinalIgnoreCase);


	[PublicAPI]
	public static T? CreateFromExpando<T>(ExpandoObject? exp) where T:ObjectTemplate,new(){
		if(exp==null) return null;

		var o=new T();
		foreach(var (key,value) in exp)
			if(!o.TrySetProperty(key,value))
				throw new InvalidCastException("Error setting property \""+key+"\" of "+typeof(T).Name);
		return o;
	}

	internal static ObjectTemplate? TryCreateTemplate(IEnumerable<(string key,object? value)> properties,Type targetType){
		if(!typeof(ObjectTemplate).IsAssignableFrom(targetType)) return null;

		var o=(ObjectTemplate)Activator.CreateInstance(targetType)!;
		foreach(var (key,value) in properties)
			if(!o.TrySetProperty(key,value))
				return null;
		return o;
	}

	internal static JsonObject? TryCreateJson(IEnumerable<(string key,object? value)> properties){
		var o=new JsonObject();
		foreach(var (key,value) in properties)
			if(StaticallyTypedUtils.TryCast<Json>(value,out var casted))
				o[key]=casted;
			else return null;
		return o;
	}

	internal static ExpandoObject TryCreateExpando(IEnumerable<(string key,object? value)> properties){
		var o=new ExpandoObject();
		foreach(var (key,value) in properties)
			((IDictionary<string,object?>)o)[key]=StaticallyTypedUtils.TryCast<object?>(value,out var casted)?casted:value;
		return o;
	}

	private protected virtual bool TrySetProperty(string key,object? value){
		var field=GetType().GetField(key);
		field??=GetType().GetField(key,BindingFlags.IgnoreCase);

		if(field==null){
			_extraProps[key]=value;
			return true;
		}

		if(!StaticallyTypedUtils.TryCast(value,field.FieldType,out var casted)) return false;

		field.SetValue(this,casted);
		return true;
	}

	protected internal virtual IEnumerable<(string key,object? value)> GetProperties(){
		foreach(var field in GetType().GetFields()){
			if(field.IsStatic) continue;
			if(field.IsPrivate) continue;

			yield return (field.Name,field.GetValue(this));
		}
		foreach(var (key,value) in _extraProps) yield return (key,value);
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
}