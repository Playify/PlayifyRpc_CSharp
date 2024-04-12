using System.Diagnostics;
using System.Reflection;
using System.Text;
using PlayifyRpc.Internal;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types;

public partial class RpcException{
	private static string GetOwnStackTrace(Exception e){
		var str=new StringBuilder();
		var lines=FixString(new StackTrace(e,true).ToString()).Split('\n');
		foreach(var line in lines)
			if(line!=""&&!HiddenMethods.Any(line.Substring(line.IndexOf(' ')+1).StartsWith))
				str.Append('\n').Append(line);
		return str.ToString();
	}

	public static RpcException Convert(Exception e,bool freeze){
		if(e is RpcException rpc) return freeze?rpc.Freeze():rpc.Unfreeze();

		var type=e.GetType();
		return new RpcException(type.FullName??type.Name,
		                        null,
		                        e.Message,
		                        GetOwnStackTrace(e).TrimStart('\n'),
		                        e.InnerException){
			_prependOwnStack=!freeze,
		};
	}

	public RpcException Unfreeze(){
		if(_prependOwnStack) return this;

		var e=Read(Type,From,Message,StackTrace??"",Data);
		e._prependOwnStack=true;
		return e;
	}

	public RpcException Freeze(){
		if(!_prependOwnStack) return this;
		_prependOwnStack=false;
		_stackTrace+=GetOwnStackTrace(this);
		return this;
	}

	public RpcException Append(string s){
		if(_prependOwnStack){
			_prependOwnStack=false;
			_stackTrace+=GetOwnStackTrace(this);
		}
		_stackTrace+="\n\trpc "+s;
		return this;
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
			if(methodAndFurther.StartsWith(methodString+"(")||methodAndFurther.StartsWith(methodString+"[")){
				_stackTrace=_stackTrace.Substring(0,lastLine);
			}
		}
		return this;
	}

	public RpcException Append(string? type,string? method,object?[]? args)
		=>Append(args==null
			         ?"<<"+nameof(Rpc.CallLocal)+">>"
			         :(type??"<<null>>")+"."+(method??"<<null>>")+"("+
			          args.Select(r=>StaticallyTypedUtils.Stringify(r,false)).Join(",")+")");
}