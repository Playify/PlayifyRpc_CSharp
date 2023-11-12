using JetBrains.Annotations;
using PlayifyUtility.Jsons;

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
		var dot=s.IndexOf('.');
		if(dot==-1) throw new FormatException("No dot");
		var bracket=s.IndexOf('(',dot+1);
		if(bracket==-1) throw new FormatException("No opening bracket");
		if(s[^1]!=')') throw new FormatException("No closing bracket");
		
		var type=s[..dot];
		var method=s[(dot+1)..bracket];
		var args=new List<object?>();

		var argsString=s[(bracket+1)..^1];
		var argsStrings=argsString.Trim().Length==0?Array.Empty<string>():argsString.Split(',');
		using var enumerator=((IEnumerable<string>)argsStrings).GetEnumerator();
		while(enumerator.MoveNext()){
			var argString=enumerator.Current;

			var obj=ParseParameter(argString.Trim());
			while(obj==NoValue){
				if(!enumerator.MoveNext()) throw new FormatException("Error parsing arguments");
				argString+=","+enumerator.Current;
				obj=ParseParameter(argString.Trim());
			}
			args.Add(obj);
		}

		return await Rpc.CallFunction(type,method,args.ToArray());
	}

	public static async Task<string> Eval(string s){
		var result=await EvalAny(s);
		
		if(StaticallyTypedUtils.TryCast<Json>(result,out var json))
			return json.ToString("\t");
		
		return result switch{
			null=>"null",
			float.NaN or double.NaN=>"NaN",
			_=>result.ToString()??"",
		};
	}
}