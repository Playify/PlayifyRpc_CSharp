using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using PlayifyRpc.Connections;

namespace PlayifyRpc.Types;

public class RpcException:Exception{
	private static readonly Regex TabRegex=new("^  +",RegexOptions.Multiline);
	public readonly string Type;
	public readonly string From;

	public static string AsString(Exception e)=>e.ToString();

	public RpcException(string message,Exception cause):base(message,cause){
		Type=nameof(RpcException);
		From=ClientConnection.Instance is ServerConnectionLoopbackClient?"SERVER":Rpc.Id;
		StackTrace=TabRegex.Replace("caused by: "+AsString(cause).Replace("\r",""),
		                            "\t");
	}

	public RpcException(Exception wrap):base(wrap.Message,wrap.InnerException){
		if(wrap is RpcException re){
			Type=re.Type;
			From=re.From;
			StackTrace=re.StackTrace;
		} else{
			Type=wrap.GetType().Name;
			From=ClientConnection.Instance is ServerConnectionLoopbackClient?"SERVER":Rpc.Id;
			var stackTrace=new StackTrace(wrap,true).ToString().Replace("\r","");
			if(wrap.InnerException!=null) stackTrace+="\ncaused by: "+wrap.InnerException;
			StackTrace=TabRegex.Replace(stackTrace,"\t");
		}
	}

	public RpcException(string? type,string from,string? message,string? stackTrace):base(message){
		Type=type??nameof(RpcException);
		From=from;
		StackTrace=TabRegex.Replace((stackTrace??"").Replace("\r",""),"\t");
	}


	public override string StackTrace{get;}

	public override string ToString(){
		var str=new StringBuilder();
		str.Append(Type);
		str.Append('(').Append(From).Append(')');

		if(!string.IsNullOrWhiteSpace(Message)) str.Append(": ").Append(Message);

		var st=StackTrace;
		if(!string.IsNullOrEmpty(st)) str.Append('\n').Append(st);

		return str.ToString().Replace("\n","\r\n");
	}
}