using System.Reflection;
using PlayifyRpc.Types.Exceptions;

namespace PlayifyRpc.Internal.Data;

public partial class DynamicBinder{
	internal static object? Invoke(Delegate func,string? type,string method,RpcDataPrimitive[] args){
		try{
			CurrentMethod.Value=func.Method;
			const BindingFlags all=BindingFlags.Public|
			                       BindingFlags.NonPublic|
			                       BindingFlags.OptionalParamBinding|
			                       BindingFlags.FlattenHierarchy|
			                       BindingFlags.Static|
			                       BindingFlags.Instance|
			                       BindingFlags.InvokeMethod;
			return func.Method.DeclaringType!.InvokeMember(func.Method.Name,all,Instance,func.Target,args.Cast<object>().ToArray(),null!);
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
		} finally{
			CurrentMethod.Value=null;
		}
	}

	internal static object? InvokeMeta(Delegate func,string? type,string meta,RpcDataPrimitive[] args){
		try{
			CurrentMethod.Value=func.Method;
			const BindingFlags all=BindingFlags.Public|
			                       BindingFlags.NonPublic|
			                       BindingFlags.OptionalParamBinding|
			                       BindingFlags.FlattenHierarchy|
			                       BindingFlags.Static|
			                       BindingFlags.Instance|
			                       BindingFlags.InvokeMethod;
			return func.Method.DeclaringType!.InvokeMember(func.Method.Name,all,Instance,func.Target,args.Cast<object>().ToArray(),null!);
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
		} finally{
			CurrentMethod.Value=null;
		}
	}
}