using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Utils.Extensions;

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
	internal static async Task<Task<RpcDataPrimitive>> Eval(string expression,Func<Task<string>>? postArgsProvider=null,bool isFromRequest=false,IEnumerable<RpcDataPrimitive>? appendArgs=null){
		appendArgs??=[];
		//Try to use postArgs if expression is not available
		if(expression==""&&postArgsProvider!=null){
			expression=await postArgsProvider();
			postArgsProvider=null;
		}
		//Try parse from raw array
		if(expression.Length!=0&&expression[0]=='['&&expression[expression.Length-1]==']'){
			var parsed=RpcDataPrimitive.Parse(expression)??throw new RpcEvalException("Error parsing expression");
			if(!parsed.IsArray(out var enumerable,out var length)) throw new RpcEvalException("Error parsing expression array");
			if(postArgsProvider!=null){
				var arr2=RpcDataPrimitive.Parse(await postArgsProvider())??throw new RpcEvalException("Error parsing expression from POST request");
				if(!arr2.IsArray(out var extras,out var extrasLength)) throw new RpcEvalException("Error parsing expression array from POST request");
				enumerable=enumerable.Concat(extras);
				length+=extrasLength;
			}

			if(length<2) throw new RpcEvalException("Expression array not long enough");
			var array=enumerable.ToArray();
			if(!array[0].TryTo<string>(out var typeFromArray)) throw new RpcEvalException("Error getting type from expression array");
			if(!array[1].TryTo<string>(out var methodFromArray)) throw new RpcEvalException("Error getting method from expression array");

			if(typeFromArray==null) throw new NullReferenceException("Type is null");
			return Invoker.CallFunctionRaw(typeFromArray,methodFromArray,array.Skip(2).Concat(appendArgs).ToArray());
		}

		var bracket=expression.IndexOf('(');
		if(bracket==-1){
			if(isFromRequest&&expression.Contains('/')) throw new RpcEvalException("Type or name should not contain a '/', as it would interfere with URL parameters like /void or /pretty");

			if(expression.Length==0)//postArgs already handled
				return Rpc.GetAllTypes().Then(x=>RpcDataPrimitive.From(x));
			if(expression[expression.Length-1]=='.')
				if(postArgsProvider!=null) throw new RpcEvalException("Can't use POST data for this");
				else
					return Rpc.CreateObject(expression.Substring(0,expression.Length-1)).GetMethods()
					          .Then(x=>RpcDataPrimitive.From(x));
			if(expression[expression.Length-1]=='?')
				if(postArgsProvider!=null) throw new RpcEvalException("Can't use POST data for this");
				else
					return Rpc.CreateObject(expression.Substring(0,expression.Length-1)).Exists()
					          .Then(x=>RpcDataPrimitive.From(x));

			var dotPos=expression.LastIndexOf('.');
			if(dotPos==-1) throw new RpcEvalException("No opening bracket");

			if(postArgsProvider==null)
				return Rpc.CreateFunction(expression.Substring(0,dotPos),expression.Substring(dotPos+1)).GetSignatures()
				          .Then(x=>RpcDataPrimitive.From(x));

			var postArgs=(await postArgsProvider()).Trim();
			if(postArgs.Length==0||postArgs[0]!='['||postArgs[postArgs.Length-1]!=']')
				throw new RpcEvalException("POST data needs to be an array when used as arguments");

			bracket=expression.Length;
			expression+=$"({postArgs.Substring(1,postArgs.Length-2)})";
			postArgsProvider=null;
		}
		if(expression[expression.Length-1]!=')') throw new RpcEvalException("No closing bracket");
		if(isFromRequest&&expression.IndexOf('/',0,bracket)!=-1)
			throw new RpcEvalException("Type or name should not contain a '/', as it would interfere with URL parameters like /void or /pretty");
		var dot=expression.LastIndexOf('.',bracket,bracket);
		if(dot==-1) throw new RpcEvalException("No dot");

		var type=expression.Substring(0,dot);
		var method=expression.Substring(dot+1,bracket-dot-1);
		var args=new List<RpcDataPrimitive>();


		var argsString=expression.Substring(bracket+1,expression.Length-(bracket+1)-1);
		if(postArgsProvider!=null)
			if(argsString=="") argsString=await postArgsProvider();
			else argsString+=","+await postArgsProvider();

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
		return Invoker.CallFunctionRaw(type,method,args.Concat(appendArgs).ToArray());
	}

	internal static async Task<RpcDataPrimitive> EvalObject(string expression)=>await await Eval(expression);
	internal static async Task<string> EvalString(string expression,bool pretty)=>(await await Eval(expression)).ToString(pretty);
}