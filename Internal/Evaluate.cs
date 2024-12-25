using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Invokers;

namespace PlayifyRpc.Internal;

/**
 * Allows parsing strings as rpc-calls
 *
 * Type.method(arg1,arg2) => will call the function
 * ["Type","Method",arg1,arg2] => will call the function as well
 * Type. => will get all methods on the type
 * Type? => will check if the type exists
 * Type.Method => will get the method signatures
 *
 * arguments should be Json
 */
internal static class Evaluate{
	internal static async Task<RpcDataPrimitive> EvalObject(string expression,string? postArgs=null){
		//Try to use postArgs if expression is not available
		if(expression==""&&postArgs!=null) (expression,postArgs)=(postArgs,null);
		//Try parse from raw array
		if(expression.Length!=0&&expression[0]=='['&&expression[expression.Length-1]==']'){
			var parsed=RpcDataPrimitive.Parse(expression)??throw new RpcEvalException("Error parsing expression");
			if(!parsed.IsArray(out var enumerable,out var length)) throw new RpcEvalException("Error parsing expression array");
			if(length<2) throw new RpcEvalException("Expression array not long enough");
			var array=enumerable.ToArray();
			if(!array[0].TryTo<string>(out var typeFromArray)) throw new RpcEvalException("Error getting type from expression array");
			if(!array[1].TryTo<string>(out var methodFromArray)) throw new RpcEvalException("Error getting method from expression array");

			if(typeFromArray==null) throw new NullReferenceException("Type is null");
			return await Invoker.CallFunctionRaw(typeFromArray,methodFromArray,array.Skip(2).ToArray());
		}

		var bracket=expression.IndexOf('(');
		if(bracket==-1){
			if(expression.Length==0)//postArgs already handled
				return RpcDataPrimitive.From(await Rpc.GetAllTypes(),null);
			if(expression[expression.Length-1]=='.')
				if(postArgs!=null) throw new RpcEvalException("Can't use POST data for this");
				else return RpcDataPrimitive.From(await Rpc.CreateObject(expression.Substring(0,expression.Length-1)).GetMethods(),null);
			if(expression[expression.Length-1]=='?')
				if(postArgs!=null) throw new RpcEvalException("Can't use POST data for this");
				else return RpcDataPrimitive.From(await Rpc.CreateObject(expression.Substring(0,expression.Length-1)).Exists(),null);

			var dotPos=expression.LastIndexOf('.');
			if(dotPos==-1) throw new RpcEvalException("No opening bracket");

			if(postArgs==null)
				return RpcDataPrimitive.From(await Rpc.CreateFunction(expression.Substring(0,dotPos),expression.Substring(dotPos+1)).GetMethodSignatures(),null);

			postArgs=postArgs.Trim();
			if(postArgs.Length==0||postArgs[0]!='['||postArgs[postArgs.Length-1]!=']')
				throw new RpcEvalException("POST data needs to be an array when used as arguments");

			bracket=expression.Length;
			expression+=$"({postArgs.Substring(1,postArgs.Length-2)})";
			postArgs=null;
		}
		if(expression[expression.Length-1]!=')') throw new RpcEvalException("No closing bracket");
		var dot=expression.LastIndexOf('.',bracket,bracket);
		if(dot==-1) throw new RpcEvalException("No dot");

		var type=expression.Substring(0,dot);
		var method=expression.Substring(dot+1,bracket-dot-1);
		var args=new List<RpcDataPrimitive>();


		var argsString=expression.Substring(bracket+1,expression.Length-(bracket+1)-1);
		if(postArgs!=null)
			if(argsString=="") argsString=postArgs;
			else argsString+=","+postArgs;

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
		return await Invoker.CallFunctionRaw(type,method,args.ToArray());
	}

	internal static async Task<string> EvalString(string expression,bool pretty)=>(await EvalObject(expression)).ToString(pretty);
}