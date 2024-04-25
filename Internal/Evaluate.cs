using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Jsons;

namespace PlayifyRpc.Internal;

/**
 * Allows parsing strings as rpc-calls
 *
 * Type.method(arg1,arg2) => will call the function
 * Type. => will get all methods on the type
 * Type? => will check if the type exists
 *
 * arguments should be Json
 */
internal static class Evaluate{
	private static readonly object NoValue=new();

	private static object? ParseParameter(string argString){
		if(int.TryParse(argString,out var i)) return i;
		if(double.TryParse(argString,out var d)) return d;
		if(long.TryParse(argString,out var l)) return l;
		if(Json.TryParse(argString,out var json)) return json;

		return NoValue;
	}

	internal static async Task<object?> EvalAny(string s){
		var bracket=s.IndexOf('(');
		if(bracket==-1){
			if(s=="")
				return await Rpc.GetAllTypes();
			if(s.EndsWith("."))
				return await Rpc.CreateObject(s.Substring(0,s.Length-1)).GetMethods();
			if(s.EndsWith("?"))
				return await Rpc.CreateObject(s.Substring(0,s.Length-1)).Exists();

			throw new RpcEvalException("No opening bracket");
		}
		if(!s.EndsWith(")")) throw new RpcEvalException("No closing bracket");
		var dot=s.LastIndexOf('.',bracket,bracket);
		if(dot==-1) throw new RpcEvalException("No dot");

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
				} else throw new RpcEvalException("Error parsing arguments");
			args.Add(obj);
		}
		return await Rpc.CallFunction(type,method,args.ToArray());
	}

	internal static async Task<string> Eval(string s,bool pretty)=>StaticallyTypedUtils.Stringify(await EvalAny(s),pretty);
}