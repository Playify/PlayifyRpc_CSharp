using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;

namespace PlayifyRpc.Types;

public readonly struct RpcFunction{
	public readonly string Type;
	public readonly string Method;

	public RpcFunction(string type,string method){
		Type=type;
		Method=method;
	}

	[PublicAPI]
	public PendingCall Call(params object?[] args)=>Rpc.CallFunction(Type,Method,args);

	[PublicAPI]
	public PendingCall<T> Call<T>(params object?[] args)=>Rpc.CallFunction<T>(Type,Method,args);

	[PublicAPI]
	public PendingCall<RpcDataPrimitive> CallRaw(RpcDataPrimitive[] args)=>Rpc.CallFunctionRaw(Type,Method,args);

	public async Task<(string[] parameters,string returns)[]> GetMethodSignatures(bool typeScript=false)=>
		await FunctionCallContext.CallFunction<(string[] parameters,string returns)[]>(Type,null,"S",Method,typeScript);


	public static RpcFunction RegisterFunction(Delegate func){
		lock(StringToFunc){
			if(!FuncToString.TryGetValue(func,out var id)){
				id=(_id++).ToString("x");
				StringToFunc.Add(id,func);
				FuncToString.Add(func,id);
			}
			return new RpcFunction(RegisteredTypeName,id);
		}
	}

	public static void UnregisterFunction(Delegate func){
		lock(StringToFunc)
			if(FuncToString.Remove(func,out var id))
				StringToFunc.Remove(id);
	}

	public static void UnregisterFunction(RpcFunction func){
		if(func.Type!=RegisteredTypeName) throw new ArgumentException("Can't unregister RemoteFunction, that was not registered locally");
		lock(StringToFunc)
			if(StringToFunc.Remove(func.Method,out var del))
				FuncToString.Remove(del);
	}

	private static readonly Dictionary<string,Delegate> StringToFunc=new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<Delegate,string> FuncToString=new();
	private static long _id=DateTime.Now.Ticks;
	private static readonly string RegisteredTypeName="$"+Rpc.Id;

	static RpcFunction()=>RegisteredTypes.Register(RegisteredTypeName,new DictionaryInvoker(StringToFunc)).ConfigureAwait(false);
}