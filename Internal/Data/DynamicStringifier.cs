using System.Dynamic;
using JetBrains.Annotations;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public static class DynamicStringifier{
	public static readonly List<Func<object?,bool,string?>> Stringifiers=[
		(o,_)=>o==null?"null":null,
		(o,_)=>o is float.NaN or double.NaN?"NaN":null,
		(o,_)=>o is char c?$"'{c}'":null,
		(o,pretty)=>DynamicCaster.TryCast(o,out Json json)?json.ToString(pretty?"\t":null):null,
		(o,_)=>DynamicCaster.TryCast(o,out string s)?s:null,
		DefaultStringifiers.Expando,
		DefaultStringifiers.Array,
	];

	public static string Stringify(object? result,bool pretty){
		foreach(var stringifier in Stringifiers)
			if(stringifier(result,pretty) is{} s)
				return s;
		return result?.ToString()??"";
	}


	private static class DefaultStringifiers{
		public static string? Expando(object? result,bool pretty){
			if(!DynamicCaster.TryCast(result,out ExpandoObject expando)) return null;

			if(!expando.Any()) return "{}";
			if(pretty)
				return ("{\n"+expando
				              .Select(pair=>$"{
					              JsonString.Escape(pair.Key)
				              }:{
					              Stringify(pair.Value,true)
				              }")
				              .Join(",\n")
				       ).Replace("\n","\n\t")+"\n}";
			return "{"+expando
			           .Select(pair=>$"{
				           JsonString.Escape(pair.Key)
			           }:{
				           Stringify(pair.Value,false)
			           }")
			           .Join(",")+"}";
		}

		public static string? Array(object? result,bool pretty){
			if(!DynamicCaster.TryCast(result,out Array array)) return null;

			if(array.Length==0) return "[]";
			if(pretty)
				return ("[\n"+array
				              .Cast<object?>()
				              .Select(o=>Stringify(o,true))
				              .Join(",\n")
				       ).Replace("\n","\n\t")+"\n]";
			return "["+array
			           .Cast<object?>()
			           .Select(o=>Stringify(o,false))
			           .Join(",")+"]";
		}
	}
}