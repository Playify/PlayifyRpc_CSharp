using PlayifyRpc.Internal.Utils;

namespace PlayifyRpc.Types.Exceptions;

public abstract class RpcCallException:RpcException{
	protected RpcCallException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}
}

[RpcCustomException("$type")]
public class RpcTypeNotFoundException:RpcCallException{
	protected internal RpcTypeNotFoundException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}

	public RpcTypeNotFoundException(string? type)
		:base(null,null,$"Type {Utility.Quoted(type)} does not exist",""){
		Data["type"]=type;
	}
}

[RpcCustomException("$method")]
public class RpcMethodNotFoundException:RpcCallException{
	protected RpcMethodNotFoundException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}

	public RpcMethodNotFoundException(string? type,string? method)
		:base(null,null,$"Method {Utility.Quoted(method)} does not exist on type {Utility.Quoted(type)}",""){
		Data["type"]=type;
		Data["method"]=method;
	}
}

[RpcCustomException("$method-meta")]
public class RpcMetaMethodNotFoundException:RpcMethodNotFoundException{
	protected RpcMetaMethodNotFoundException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}

	public RpcMetaMethodNotFoundException(string? type,string? meta)
		:base(null,null,$"Meta-method {Utility.Quoted(meta)} does not exist on type {Utility.Quoted(type)}",""){
		Data["type"]=type;
		Data["method"]=null;
		Data["meta"]=meta;
	}
}

[RpcCustomException("$connection")]
public sealed class RpcConnectionException:RpcCallException{
	internal RpcConnectionException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}
	public RpcConnectionException(string? message,bool stack):base(null,null,message,stack?null:""){}
}

[RpcCustomException("$eval")]
public sealed class RpcEvalException:RpcCallException{
	internal RpcEvalException(string? type,string? from,string? message,string? stackTrace):base(type,from,message,stackTrace){}
	public RpcEvalException(string message):base(null,null,message,""){}
}