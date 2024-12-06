using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Exceptions;

[PublicAPI]
public partial class RpcException:Exception{
	private static readonly Regex TabRegex=new("^  +",RegexOptions.Multiline);
	public readonly string Type;

	public readonly string From;
	public new readonly JsonObject Data=new();

	public override string? StackTrace{
		get{
			if(_prependOwnStack&&base.StackTrace==null) return null;

			var result=_stackTrace;
			if(_prependOwnStack) result+=GetOwnStackTrace(this);
			result+=_causes;
			return result.TrimStart('\n');
		}
	}
	private bool _prependOwnStack;
	private string? _stackTrace;
	private readonly string _causes="";

	public RpcException(string message):this(null,null,message,null,null){}
	public RpcException(string message,Exception cause):this(null,null,message,null,cause){}
	public RpcException(string? type,string? from,string? message,string? stackTrace):this(type,from,message,stackTrace,null){}

	public RpcException(string? type,string? from,string? message,string? stackTrace,Exception? cause):base(message,cause){
		Type=type??GetType().Name;
		if((GetType().GetCustomAttribute<RpcCustomExceptionAttribute>()?.TypeTag).NotNull(out var typeTag)) Data["$type"]=typeTag;
		From=from??Rpc.PrettyName;

		if(stackTrace==null) _prependOwnStack=true;
		else{
			_stackTrace="\n"+FixString(stackTrace);
			var causeIndex=_stackTrace.IndexOf("\ncaused by: ",StringComparison.Ordinal);
			if(causeIndex!=-1){
				_causes+=_stackTrace.Substring(causeIndex);
				_stackTrace=_stackTrace.Substring(0,causeIndex);
			}
		}

		if(cause!=null)
			_causes+="\ncaused by: "+FixString(
#if NETFRAMEWORK
				         cause is RpcException rpc?rpc.ToString():AsyncFriendlyStackTrace.ExceptionExtensions.ToAsyncString(cause)
#else
				         cause.ToString()
#endif
			         );
	}


	public override string ToString(){
		var str=new StringBuilder();
		str.Append(Type).Append('(').Append(From).Append(')');
		if(!string.IsNullOrWhiteSpace(Message)) str.Append(": ").Append(Message);

		var trace=StackTrace;
		if(!string.IsNullOrWhiteSpace(trace)) str.Append('\n').Append(trace);

		return str.ToString().Replace("\n","\r\n");
	}

	private static string FixString(string s){
		return TabRegex.Replace(s.Replace("\r","").Trim('\n'),"\t");
	}
}