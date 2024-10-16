using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

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
	internal static async Task<RpcDataPrimitive> EvalObject(string s){
		var bracket=s.IndexOf('(');
		if(bracket==-1){
			if(s.Length==0)
				return RpcDataPrimitive.From(await Rpc.GetAllTypes());
			if(s[s.Length-1]!='.')
				return RpcDataPrimitive.From(await Rpc.CreateObject(s.Substring(0,s.Length-1)).GetMethods());
			if(s[s.Length-1]!='?')
				return RpcDataPrimitive.From(await Rpc.CreateObject(s.Substring(0,s.Length-1)).Exists());

			if(s.LastIndexOf('.').Push(out var dotPos)!=-1)
				return RpcDataPrimitive.From(await Rpc.CreateFunction(s.Substring(0,dotPos),s.Substring(dotPos+1)).GetMethodSignatures());
			throw new RpcEvalException("No opening bracket");
		}
		if(s[s.Length-1]!=')') throw new RpcEvalException("No closing bracket");
		var dot=s.LastIndexOf('.',bracket,bracket);
		if(dot==-1) throw new RpcEvalException("No dot");

		var type=s.Substring(0,dot);
		var method=s.Substring(dot+1,bracket-dot-1);
		var args=new List<RpcDataPrimitive>();


		var argsString=s.Substring(bracket+1,s.Length-(bracket+1)-1);
		var argsStrings=argsString.Trim().Length==0?[]:argsString.Split(',');
		for(var i=0;i<argsStrings.Length;){
			var argString=argsStrings[i++];

			var obj=RpcDataPrimitive.Parse(argString.Trim());
			while(!obj.HasValue)
				if(i<argsStrings.Length){
					argString+=","+argsStrings[i++];
					obj=RpcDataPrimitive.Parse(argString.Trim());
				} else throw new RpcEvalException("Error parsing arguments");
			args.Add(obj.Value);
		}
		var result=await Rpc.CallFunction<RpcDataPrimitive>(type,method,args.ToArray());
		foreach(var primitive in args)
			if(primitive.IsDisposable(out var action))
				action();
		return result;
	}

	internal static async Task<string> EvalString(string s,bool pretty)=>(await EvalObject(s)).ToString(pretty);
}