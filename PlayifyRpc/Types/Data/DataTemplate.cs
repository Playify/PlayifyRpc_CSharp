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

	private static DataTemplate CreateFromExpando(ExpandoObject exp,Type targetType){
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

	public void WriteDynamic(DataOutput output,List<object> already){
		already.Add(this);
		
		var fields=GetType().GetFields().Where(f=>!f.IsStatic&&!f.IsPrivate).ToArray();
		var count=fields.Length+_extraProps.Count;
		output.WriteLength(-(count*4+2));
		
		foreach(var field in GetType().GetFields()){
			if(field.IsStatic) continue;
			if(field.IsPrivate) continue;

			output.WriteString(field.Name);
			output.WriteDynamic(field.GetValue(this),already);
		}
		foreach(var (key,value) in _extraProps){
			output.WriteString(key);
			output.WriteDynamic(value,already);
		}
	}


	private static readonly Type[] ValueTupleTypes={
		typeof(ValueTuple),
		typeof(ValueTuple<>),
		typeof(ValueTuple<,>),
		typeof(ValueTuple<,,>),
		typeof(ValueTuple<,,,>),
		typeof(ValueTuple<,,,,>),
		typeof(ValueTuple<,,,,,>),
		typeof(ValueTuple<,,,,,,>),
		typeof(ValueTuple<,,,,,,,>),
	};

	public static bool CanCast(Type? from,Type to,object? fromInstance){
		if(to.IsPrimitive) return from!=null&&DynamicBinder.CanChangePrimitive(fromInstance?.GetType(),to);

		if(from==null) return true;

		if(to.IsAssignableFrom(from)) return true;

		if(from.IsCOMObject&&to.IsInstanceOfType(fromInstance)) return true;

		if(from.IsArray&&fromInstance is Array array){
			if(to.IsArray){
				var elementType=to.GetElementType()!;
				return array.Cast<object?>().All(o=>CanCast(o?.GetType(),elementType,o));
			}
			if(to.IsGenericType&&ValueTupleTypes.Contains(to.GetGenericTypeDefinition())){
				var types=to.GetGenericArguments();
				if(array.Length!=types.Length) return false;//Length mismatch
				return array.Cast<object?>().Zip(types).All(tuple=>CanCast(tuple.First?.GetType(),tuple.Second,tuple.First));
			}
		}

		if(fromInstance is ExpandoObject&&to.IsAssignableTo(typeof(DataTemplate))) return true;


		return false;

	}

	public static T DoCast<T>(object? value)=>(T)DoCast(value,typeof(T));//TODO move method to static typed utils


	public static object DoCast(object? value,Type type){//TODO clone from work
		if(value==null) return value!;
		if(type.IsPrimitive) return value;
		if(type.IsInstanceOfType(value)) return value;

		if(type.IsArray&&value is Array array){
			var elementType=type.GetElementType()!;
			var target=Array.CreateInstance(elementType,array.Length);
			var i=0;
			foreach(var o in array){
				target.SetValue(DoCast(o,elementType),i);
				i++;
			}
			return target;
		}
		if(type.IsGenericType&&ValueTupleTypes.Contains(type.GetGenericTypeDefinition())&&value is Array tupleArray){
			var constructor=type.GetConstructor(type.GetGenericArguments());
			var parameters=tupleArray.Cast<object?>().Zip(type.GetGenericArguments(),DoCast).ToArray();
			return constructor!.Invoke(parameters);
		}

		if(type.IsAssignableTo(typeof(DataTemplate))&&value is ExpandoObject expando) return CreateFromExpando(expando,type);

		throw new Exception("Error casting \""+value+"\" to "+type.Name);
	}
}