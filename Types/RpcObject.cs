using System.Dynamic;
using System.Linq.Expressions;
using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Types.Invokers;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types;

[PublicAPI]
public readonly struct RpcObject(string type):IDynamicMetaObjectProvider,IEquatable<RpcObject>{
	public readonly string Type=type;

	public RpcFunction GetFunction(string name)=>new(Type,name);
	public PendingCall CallFunction(string name,params object?[] args)=>Rpc.CallFunction(Type,name,args);
	public PendingCall<T> CallFunction<T>(string name,params object?[] args)=>Rpc.CallFunction<T>(Type,name,args);
	public PendingCall<RpcDataPrimitive> CallFunctionRaw(string name,RpcDataPrimitive[] args)=>Rpc.CallFunctionRaw(Type,name,args);

	public Task<string[]> GetMethods()=>Invoker.CallFunction<string[]>(Type,null,"M");
	public async Task<RpcFunction[]> GetFunctions()=>(await GetMethods()).Select(GetFunction).ToArray();
	public Task<string> GetRpcVersion()=>Invoker.CallFunction<string>(Type,null,"V");
	public Task<bool> Exists()=>Invoker.CallFunction<bool>(null,"E",Type);

	public bool RegisteredLocally{
		get{
			lock(RegisteredTypes.Registered)
				return RegisteredTypes.Registered.ContainsKey(Type);
		}
	}


	#region Equality
	public bool Equals(RpcObject other)=>Type==other.Type;
	public override bool Equals(object? obj)=>obj is RpcObject other&&Equals(other);
	public override int GetHashCode()=>Type.GetHashCode();
	public static bool operator ==(RpcObject left,RpcObject right)=>left.Equals(right);
	public static bool operator !=(RpcObject left,RpcObject right)=>!(left==right);
	#endregion

	#region MetaObject
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
				var cnt=>throw new Exception("To many generic arguments: "+cnt+" generics found)"),
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
	#endregion

}