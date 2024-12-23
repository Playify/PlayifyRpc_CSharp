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

	//Void
	public static PendingCall CallLocal(Action func)=>CallLocal(_=>func());
	public static PendingCall CallLocal(Action<FunctionCallContext> func)=>Invoker.CallLocal(func);
	public static PendingCall CallLocal(Func<Task> func)=>Invoker.CallLocal(func).Void();
	public static PendingCall CallLocal(Func<FunctionCallContext,Task> func)=>Invoker.CallLocal(func).Void();

	//Primitive
	public static PendingCall<RpcDataPrimitive> CallLocal(Func<object?> func)=>Invoker.CallLocal(func);
	public static PendingCall<RpcDataPrimitive> CallLocal(Func<FunctionCallContext,object?> func)=>Invoker.CallLocal(func);
	public static PendingCall<RpcDataPrimitive> CallLocal(Func<Task<object?>> func)=>Invoker.CallLocal(func);

	public static PendingCall<RpcDataPrimitive> CallLocal(Func<FunctionCallContext,Task<object?>> func)=>Invoker.CallLocal(func);

	//Generic
	public static PendingCall<T> CallLocal<T>(Func<object?> func)=>CallLocal(func).Cast<T>();
	public static PendingCall<T> CallLocal<T>(Func<FunctionCallContext,object?> func)=>CallLocal(func).Cast<T>();
	public static PendingCall<T> CallLocal<T>(Func<Task<object?>> func)=>CallLocal(func).Cast<T>();
	public static PendingCall<T> CallLocal<T>(Func<FunctionCallContext,Task<object?>> func)=>CallLocal(func).Cast<T>();

	public static PendingCall CallFunction(string type,string method,params object?[] args)=>Invoker.CallFunction(type,method,args);
	public static PendingCall<T> CallFunction<T>(string type,string method,params object?[] args)=>Invoker.CallFunction<T>(type,method,args);
	public static PendingCall<RpcDataPrimitive> CallFunctionRaw(string type,string method,RpcDataPrimitive[] args)=>Invoker.CallFunctionRaw(type,method,args);

	public static Task<string> EvalString(string expression,bool pretty=true)=>Evaluate.EvalString(expression,pretty);
	public static Task<RpcDataPrimitive> EvalObject(string expression)=>Evaluate.EvalObject(expression);
	public static async Task<T?> EvalObject<T>(string expression)=>(await Evaluate.EvalObject(expression)).To<T>();
}