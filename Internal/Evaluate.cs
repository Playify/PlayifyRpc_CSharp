using System.Dynamic;
using JetBrains.Annotations;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

[PublicAPI]
internal static class Evaluate{
	private static readonly object NoValue=new();

	private static object? ParseParameter(string argString){
		if(int.TryParse(argString,out var i)) return i;
		if(double.TryParse(argString,out var d)) return d;
		if(long.TryParse(argString,out var l)) return l;
		if(Json.TryParse(argString,out var json)) return json;

		return NoValue;
	}

	public static async Task<object?> EvalAny(string s){
		var bracket=s.IndexOf('(');
		if(bracket==-1){
			if(s=="")
				return await Rpc.GetAllTypes();
			if(s.EndsWith("."))
				return await Rpc.CreateObject(s.Substring(0,s.Length-1)).GetMethods();
			if(s.EndsWith("?"))
				return await Rpc.CreateObject(s.Substring(0,s.Length-1)).Exists();

			throw new FormatException("No opening bracket");
		}
		if(!s.EndsWith(")")) throw new FormatException("No closing bracket");
		var dot=s.LastIndexOf('.',bracket,bracket);
		if(dot==-1) throw new FormatException("No dot");

		var type=s.Substring(0,dot);
		var method=s.Substring(dot+1,bracket-dot-1);
		var args=new List<object?>();


		var argsString=s.Substring(bracket+1,s.Length-(bracket+1)-1);
		var argsStrings=argsString.Trim().Length==0?Array.Empty<string>():argsString.Split(',');
		for(var i=0;i<argsStrings.Length;){
			var argString=argsStrings[i++];

			var obj=ParseParameter(argString.Trim());
			while(obj==NoValue)
				if(i<argsStrings.Length){
					argString+=","+argsStrings[i++];
					obj=ParseParameter(argString.Trim());
				} else throw new FormatException("Error parsing arguments");
			args.Add(obj);
		}
		return await Rpc.CallFunction(type,method,args.ToArray());
	}

	public static async Task<string> Eval(string s)=>Stringify(await EvalAny(s));

	private static string Stringify(object? result){

		if(StaticallyTypedUtils.TryCast<Json>(result,out var json))
			return json.ToString("\t");

		return result switch{
			ExpandoObject expando when !expando.Any()=>"{}",
			ExpandoObject expando=>(
				                       "{\n"+expando
				                             .Select(pair=>JsonString.Escape(pair.Key)+":"+Stringify(pair.Value))
				                             .Join(",\n")
			                       ).Replace("\n","\n\t")+"\n}",
			Array{Length: 0}=>"[]",
			Array array=>("[\n"+array.Cast<object?>().Select(Stringify)
			                         .Join(",\n")).Replace("\n","\n\t")+"\n]",
			_=>result switch{
				null=>"null",
				float.NaN or double.NaN=>"NaN",
				_=>$"{result}",
			},
		};

	}
}