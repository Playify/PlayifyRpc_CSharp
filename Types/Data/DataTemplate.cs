using System.Dynamic;
using System.Reflection;
using PlayifyUtils.Streams;

namespace PlayifyRpc.Types.Data;

public abstract class DataTemplate{
	private readonly Dictionary<string,object?> _extraProps=new(StringComparer.OrdinalIgnoreCase);


	public static T? CreateFromExpando<T>(ExpandoObject? exp) where T:DataTemplate,new(){
		if(exp==null) return null;

		var o=new T();
		foreach(var (key,value) in exp) o.SetProperty(key,value);
		return o;
	}

	public static DataTemplate CreateFromExpando(ExpandoObject exp,Type targetType){
		if(!targetType.IsAssignableTo(typeof(DataTemplate))) throw new ArgumentException(nameof(targetType));

		var o=(DataTemplate)Activator.CreateInstance(targetType)!;
		foreach(var (key,value) in exp) o.SetProperty(key,value);
		return o;
	}


	private void SetProperty(string key,object? value){
		var field=GetType().GetField(key);
		field??=GetType().GetField(key,BindingFlags.IgnoreCase);

		if(field==null){
			_extraProps[key]=value;
			return;
		}

		field.SetValue(this,value);
	}

	private IEnumerable<(string key,object? value)> GetProperties(){
		foreach(var field in GetType().GetFields()){
			if(field.IsStatic) continue;
			if(field.IsPrivate) continue;
			
			yield return (field.Name,field.GetValue(this));
		}
		foreach(var (key,value) in _extraProps){
			yield return (key,value);
		}
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