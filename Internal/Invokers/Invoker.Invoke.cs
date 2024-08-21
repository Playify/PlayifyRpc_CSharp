using System.Reflection;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Exceptions;

namespace PlayifyRpc.Internal.Invokers;

public abstract partial class Invoker{
	protected static object? Invoke(Delegate func,string? type,string method,object?[] args){
		try{
			return func.Method.Invoke(func.Target,
				BindingFlags.OptionalParamBinding|
				BindingFlags.FlattenHierarchy|
				BindingFlags.InvokeMethod,
				DynamicBinder.Instance,
				args,
				null!);
		} catch(TargetInvocationException e){
			throw RpcException.WrapAndFreeze(e.InnerException??e);
		} catch(MissingMethodException){
			throw new RpcMethodNotFoundException(type,method,"Method doesn't accept "+args.Length+" arguments");
		} catch(MethodAccessException e){
			throw new RpcMethodNotFoundException(type,method,e.Message);
		} catch(AmbiguousMatchException){
			throw new RpcMethodNotFoundException(type,method,"Call is ambiguous"){Data={{"ambiguous",true}}};
		} catch(RpcDataException e){
			throw new RpcMethodNotFoundException(type,method,"Error casting arguments",e);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e);
		}
	}

	private protected static object? InvokeMeta(Delegate func,string? type,string meta,object?[] args){

		try{
			return func.Method.Invoke(func.Target,
				BindingFlags.OptionalParamBinding|
				BindingFlags.FlattenHierarchy|
				BindingFlags.InvokeMethod,
				DynamicBinder.Instance,
				args,
				null!);
		} catch(TargetInvocationException e){
			throw RpcException.WrapAndFreeze(e.InnerException??e);
		} catch(MissingMethodException){
			throw new RpcMetaMethodNotFoundException(type,meta,"Method doesn't accept "+args.Length+" arguments");
		} catch(MethodAccessException e){
			throw new RpcMetaMethodNotFoundException(type,meta,e.Message);
		} catch(AmbiguousMatchException){
			throw new RpcMetaMethodNotFoundException(type,meta,"Call is ambiguous"){Data={{"ambiguous",true}}};
		} catch(RpcDataException e){
			throw new RpcMetaMethodNotFoundException(type,meta,"Error casting arguments",e);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e);
		}
	}


}