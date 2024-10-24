using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;

namespace PlayifyRpc;

public static partial class Rpc{
	public static RpcObject CreateObject(string type)=>new(type);
	public static RpcFunction CreateFunction(string type,string method)=>new(type,method);

	public static RpcFunction RegisterFunction(Delegate func)=>RpcFunction.RegisterFunction(func);
	public static void UnregisterFunction(Delegate func)=>RpcFunction.UnregisterFunction(func);
	public static void UnregisterFunction(RpcFunction func)=>RpcFunction.UnregisterFunction(func);

	public static PendingCall CallLocal(Action func)=>Invoker.CallLocal(func);
	public static PendingCall<RpcDataPrimitive> CallLocal(Func<object?> func)=>Invoker.CallLocal(func);
	public static PendingCall<T> CallLocal<T>(Func<object?> func)=>Invoker.CallLocal<T>(func);

	public static PendingCall CallFunction(string type,string method,params object?[] args)=>FunctionCallContext.CallFunction(type,method,args);
	public static PendingCall<T> CallFunction<T>(string type,string method,params object?[] args)=>FunctionCallContext.CallFunction<T>(type,method,args);
	public static PendingCall<RpcDataPrimitive> CallFunctionRaw(string type,string method,RpcDataPrimitive[] args)=>FunctionCallContext.CallFunctionRaw(type,method,args);

	public static Task<string> EvalString(string expression,bool pretty=true)=>Evaluate.EvalString(expression,pretty);
	public static Task<RpcDataPrimitive> EvalObject(string expression)=>Evaluate.EvalObject(expression);
	public static async Task<T?> EvalObject<T>(string expression)=>(await Evaluate.EvalObject(expression)).To<T>();

	public static FunctionCallContext GetContext()=>FunctionCallContext.GetContext();
	public static bool TryGetContext(out FunctionCallContext ctx)=>FunctionCallContext.TryGetContext(out ctx);
	public static void RunWithContext(Action func,FunctionCallContext context)=>FunctionCallContext.RunWithContext(func,context);
	public static T RunWithContext<T>(Func<T> func,FunctionCallContext context)=>FunctionCallContext.RunWithContext(func,context);
}