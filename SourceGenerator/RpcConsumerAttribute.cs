namespace PlayifyRpc.SourceGenerator;

/**
 * Used to statically type RpcObjects.
 * Partial methods will be implemented using a Source Generator to forward method calls over rpc.
 * Methods should return PendingCall, Task or ValueTask (and their generic variants), if not, then the call will be synchronously handled.
 */
[AttributeUsage(AttributeTargets.Class)]
public class RpcConsumerAttribute(string? type=null):Attribute{
	private readonly string? _type=type;
}