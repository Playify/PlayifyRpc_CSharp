using System.Dynamic;
using System.Reflection;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Types.Data.Objects;

public abstract class ObjectTemplateBase:DynamicObject{
	public abstract bool TrySetProperty(string key,object? value,bool throwOnError);
	public abstract bool TryGetProperty(string key,out object? value);
	public abstract IEnumerable<(string key,object? value)> GetProperties();


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
		if(TryGetProperty(binder.Name,out var value)&&DynamicCaster.TryCast(value,binder.ReturnType,out result!,true))
			return true;
		result=null!;
		return false;
	}

	public override bool TrySetMember(SetMemberBinder binder,object? value)=>TrySetProperty(binder.Name,value,true);

	public override IEnumerable<string> GetDynamicMemberNames()=>GetProperties().Select(t=>t.key);


	internal bool? TrySetReflectionProperty(string key,object? value,bool throwOnError){
		var type=GetType();
		if(type.GetProperty(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase) is{
			   CanWrite: true,
			   IsSpecialName: false,
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

		return null;
	}

	internal bool TryGetReflectionProperty(string key,out object? value){
		var type=GetType();
		if(type.GetProperty(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase) is{
			   CanRead: true,
			   IsSpecialName: false,
		   } property){
			value=property.GetValue(this);
			return true;
		}
		if(type.GetField(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase) is{} field){
			value=field.GetValue(this);
			return true;
		}
		value=null;
		return false;
	}

	internal IEnumerable<(string key,object? value)> GetReflectionProperties(){
		var type=GetType();
		foreach(var property in type.GetProperties()){
			if(property.IsSpecialName) continue;
			if(!property.CanRead) continue;

			yield return (property.Name,property.GetValue(this));
		}
		foreach(var field in type.GetFields()){
			if(field.IsStatic) continue;
			if(field.IsPrivate) continue;

			yield return (field.Name,field.GetValue(this));
		}
	}
}