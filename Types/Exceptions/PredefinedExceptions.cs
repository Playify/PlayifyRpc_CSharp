namespace PlayifyRpc.Types.Exceptions;

public abstract class RpcCallException:RpcException{
	protected RpcCallException(string? type,string? from,string? message,string? stackTrace,Exception? cause=null):base(type,from,message,stackTrace,cause){}

	private protected static string Quoted(string? s)=>s==null?"null":"\""+s+"\"";
}

[RpcCustomException("$type")]
public class RpcTypeNotFoundException:RpcCallException{
	protected internal RpcTypeNotFoundException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}

	public RpcTypeNotFoundException(string? type)
		:base(null,null,$"Type {Quoted(type)} does not exist",""){
		Data["type"]=type;
	}
}

[RpcCustomException("$method")]
public class RpcMethodNotFoundException:RpcCallException{
	protected RpcMethodNotFoundException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}


	public RpcMethodNotFoundException(string? type,string? method,string? message=null,Exception? cause=null)
		:base(null,null,message??$"Method {Quoted(method)} does not exist on type {Quoted(type)}","",cause){
		Data["type"]=type;
		Data["method"]=method;
	}
}

[RpcCustomException("$method-meta")]
public class RpcMetaMethodNotFoundException:RpcMethodNotFoundException{
	protected RpcMetaMethodNotFoundException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}

	public RpcMetaMethodNotFoundException(string? type,string? meta,string? message=null,Exception? cause=null)
		:base(null,null,message??$"Meta-method {Quoted(meta)} does not exist on type {Quoted(type)}",cause){
		Data["type"]=type;
		Data["method"]=null;
		Data["meta"]=meta;
	}
}

[RpcCustomException("$connection")]
public class RpcConnectionException:RpcCallException{
	internal RpcConnectionException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}
	public RpcConnectionException(string? message):base(null,null,message,""){}
}

[RpcCustomException("$eval")]
public class RpcEvalException:RpcCallException{
	internal RpcEvalException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}
	public RpcEvalException(string message):base(null,null,message,""){}
}

[RpcCustomException("$data")]
public class RpcDataException:RpcException{
	internal RpcDataException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}
	public RpcDataException(string message,Exception? cause=null):base(null,null,message,"",cause){}
}