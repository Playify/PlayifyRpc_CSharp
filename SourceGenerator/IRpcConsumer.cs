namespace PlayifyRpc.SourceGenerator;

/*
Interface does nothing when applied manually. It will be automatically added when RpcConsumerAttribute is used
RpcConsumerAttribute automatically implements RpcType, except if you already implement RpcType yourself.
*/
public interface IRpcConsumer{
	string RpcType{get;}
}