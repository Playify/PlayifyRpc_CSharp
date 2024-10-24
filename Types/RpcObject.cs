using System.Dynamic;
using System.Linq.Expressions;
using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types;

[PublicAPI]
public readonly struct RpcObject(string type):IDynamicMetaObjectProvider{
	public readonly string Type=type;

	public RpcFunction GetFunction(string name)=>new(Type,name);
	public PendingCall CallFunction(string name,params object?[] args)=>Rpc.CallFunction(Type,name,args);
	public PendingCall<T> CallFunction<T>(string name,params object?[] args)=>Rpc.CallFunction<T>(Type,name,args);
	public PendingCall<RpcDataPrimitive> CallFunctionRaw(string name,RpcDataPrimitive[] args)=>Rpc.CallFunctionRaw(Type,name,args);

	public Task<string[]> GetMethods()=>FunctionCallContext.CallFunction<string[]>(Type,null,"M");
	public Task<(string[] parameters,string returns)[]> GetMethodSignatures(string method,bool typeScript=false)=>GetFunction(method).GetMethodSignatures(typeScript);
	public Task<string> GetRpcVersion()=>FunctionCallContext.CallFunction<string>(Type,null,"V");
	public Task<bool> Exists()=>FunctionCallContext.CallFunction<bool>(null,"E",Type);


	DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)=>new MetaObject(parameter,Type);

	private class MetaObject(Expression expression,string type):DynamicMetaObject(expression,BindingRestrictions.Empty,new RpcObject(type)){

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder){
			var restrictions=BindingRestrictions.GetTypeRestriction(Expression,typeof(RpcObject));
			return new DynamicMetaObject(Expression.Convert(Expression.Constant(new RpcFunction(type,binder.Name)),typeof(object)),restrictions);
		}

		public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder,DynamicMetaObject[] args){
			var generics=binder.GetGenericTypeArguments();
			Type[]? genericList=(generics?.Count??0) switch{
				0=>null,
				1=>[generics![0]],
				var cnt=>throw new Exception("To many generic arguments "+cnt+" generics found)"),
			};

			var callExpression=Expression.Call(
				typeof(Rpc),
				nameof(Rpc.CallFunction),
				genericList,
				Expression.Constant(type),
				Expression.Constant(binder.Name),
				Expression.NewArrayInit(typeof(object),args.Select(arg=>Expression.Convert(arg.Expression,typeof(object))))
			);
			var restrictions=BindingRestrictions.GetTypeRestriction(Expression,typeof(RpcObject));
			return new DynamicMetaObject(callExpression,restrictions);
		}
	}
}