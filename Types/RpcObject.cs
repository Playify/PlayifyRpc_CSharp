using System.Dynamic;
using System.Reflection;
using PlayifyRpc.Types.Functions;

namespace PlayifyRpc.Types;

public class RpcObject:DynamicObject{
	public readonly string Type;

	public RpcObject(string type)=>Type=type;

	public RpcFunction GetFunction(string name)=>new(Type,name);
	public PendingCall CallFunction(string name,params object?[] args)=>Rpc.CallFunction(Type,name,args);
	public PendingCall<T> CallFunction<T>(string name,params object?[] args)=>Rpc.CallFunction<T>(Type,name,args);

	public override bool TryInvokeMember(InvokeMemberBinder binder,object?[]? args,out object? result){
		var list=GetGenericTypeArguments(binder)??Array.Empty<Type>();
		result=list.Count switch{
			1=>Rpc.CallFunction(Type,binder.Name,args!).Cast(list[0]),
			0=>Rpc.CallFunction(Type,binder.Name,args!),
			var cnt=>throw new Exception("To many generic arguments "+cnt+" generics found)"),
		};
		return true;
	}

	public override bool TryGetMember(GetMemberBinder binder,out object? result){
		result=GetFunction(binder.Name);
		return true;
	}

	public Task<string[]> GetMethods()=>FunctionCallContext.CallFunction<string[]>(Type,null,"M");
	public Task<(string[] parameters,string @return)[]> GetMethodSignatures(string method,bool typeScript=false)=>GetFunction(method).GetMethodSignatures(typeScript);
	public Task<bool> Exists()=>FunctionCallContext.CallFunction<bool>(null,"E",Type);


	private static IList<Type>? GetGenericTypeArguments(InvokeMemberBinder binder)
		=>System.Type.GetType("Mono.Runtime")!=null
			  ?binder
			   .GetType()
			   .GetField("typeArguments",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
			   ?.GetValue(binder) as IList<Type>
			  :binder
			   .GetType()
			   .GetInterface("Microsoft.CSharp.RuntimeBinder.ICSharpInvokeOrInvokeMemberBinder")
			   ?.GetProperty("TypeArguments")
			   ?.GetValue(binder,null) as IList<Type>;

}