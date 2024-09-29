using System.Diagnostics;
using System.Reflection;
using System.Text;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;
#if NETFRAMEWORK
using AsyncFriendlyStackTrace;
#endif

namespace PlayifyRpc.Types.Exceptions;

public partial class RpcException{
	private static readonly List<string> HiddenMethods=[
		$"{typeof(FunctionCallContext).FullName}.{nameof(FunctionCallContext.RunWithContextAsync)}(",
		$"{typeof(PendingCall).FullName}.{nameof(PendingCall.DoCast)}[",
		$"{typeof(Evaluate).FullName}.{nameof(Evaluate.EvalObject)}(",
		$"{typeof(Evaluate).FullName}.{nameof(Evaluate.EvalString)}(",
		$"{typeof(RpcWebServer).FullName}.{nameof(RpcWebServer.HandleRequest)}(",
	];

	private static string GetOwnStackTrace(Exception e){
		var str=new StringBuilder();

		var lines=FixString(
#if NETFRAMEWORK
			new StackTrace(e,true).ToAsyncString()
#else
			new StackTrace(e,true).ToString()
#endif
		).Split('\n');
		foreach(var line in lines){
			if(line=="") continue;
			var substring=line.Substring(line.IndexOf(' ')+1);
			substring=substring.RemoveFromStart("async ");
			if(!HiddenMethods.Any(substring.StartsWith)) str.Append('\n').Append(line);
		}
		return str.ToString();
	}

	public static RpcException WrapAndFreeze(Exception e){
		if(e is RpcException rpc){
			rpc.Freeze();
			return rpc;
		}

		var type=e.GetType();
		return new RpcException(type.FullName??type.Name,
			null,
			e.Message,
			GetOwnStackTrace(e).TrimStart('\n'),
			e.InnerException);
	}

	private void Freeze(){
		if(!_prependOwnStack) return;
		_prependOwnStack=false;
		_stackTrace+=GetOwnStackTrace(this);
	}

	public RpcException Unfreeze(){
		if(_prependOwnStack) return this;

		var e=Read(Type,From,Message,StackTrace??"",Data,InnerException);
		e._prependOwnStack=true;
		return e;
	}

	public RpcException Remove(MethodBase? method){
		if(_prependOwnStack){
			_prependOwnStack=false;
			_stackTrace+=GetOwnStackTrace(this);
		}
		if(method!=null&&_stackTrace!=null){
			var lastLine=_stackTrace.LastIndexOf('\n');
			var methodAndFurther=_stackTrace.Substring(_stackTrace.IndexOf(' ',lastLine)+1);
			var methodString=method.DeclaringType?.ToString().Replace('+','.')+"."+method.Name;
			if(methodAndFurther.StartsWith(methodString+"(")||methodAndFurther.StartsWith(methodString+"["))
				_stackTrace=_stackTrace.Substring(0,lastLine);
		}
		return this;
	}

	public RpcException Append(string s){
		Freeze();
		_stackTrace+="\n\trpc "+s;
		return this;
	}

	public RpcException Append(string? type,string? method,object?[]? args)
		=>Append(args==null
			         ?"<<"+nameof(Rpc.CallLocal)+">>"
			         :(type??"<<null>>")+"."+(method??"<<null>>")+"("+
			          args.Select(r=>DynamicStringifier.Stringify(r,false)).Join(",")+")");
}