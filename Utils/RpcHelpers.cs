using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.HelperClasses;

namespace PlayifyRpc.Utils;

[PublicAPI]
public static class RpcHelpers{

	#region AutoRecall
	public static void AutoRecall(MessageFunc onMessage,string type,string method,params object?[] args)=>AutoRecall(onMessage,null,type,method,args);
	public static void AutoRecall(MessageFunc onMessage,TimeSpan retryDelay,string type,string method,params object?[] args)=>AutoRecall(onMessage,null,retryDelay,type,method,args);
	public static void AutoRecall(MessageFunc onMessage,Action<RpcException?>? onError,string type,string method,params object?[] args)=>AutoRecall(onMessage,onError,TimeSpan.FromSeconds(1),type,method,args);

	public static async void AutoRecall(MessageFunc onMessage,Action<RpcException?>? onError,TimeSpan retryDelay,string type,string method,params object?[] args){
		while(true){
			RpcException? ex;
			try{
				var call=Rpc.CallFunction(type,method,args);
				_=call.AddMessageListener(onMessage);
				await call;
				ex=null;
			} catch(RpcException e){
				ex=e;
			}
			onError?.Invoke(ex);
			await Task.Delay(retryDelay);
		}
	}
	#endregion

	#region ListenValue
	public static ReferenceTo<T> ListenValue<T>(string type,string method,params object?[] args){
		var r=new ReferenceTo<T>();
		AutoRecall(msg=>r.Value=msg[0].To<T>()!,type,method,args);
		return r;
	}

	public static ReferenceTo<T> ListenValue<T>(T @default,string type,string method,params object?[] args){
		var r=new ReferenceTo<T>(@default);
		AutoRecall(msg=>r.Value=msg[0].To<T>()!,_=>r.Value=@default,type,method,args);
		return r;
	}
	#endregion

	#region ListenOnChange
	public static ReferenceTo<T> ListenOnChange<T>(Action<T> onChange,string type,string method,params object?[] args){
		var r=new ReferenceTo<T>();
		AutoRecall(msg=>{
			var newValue=msg[0].To<T>();
			if(!EqualityComparer<T?>.Default.Equals(r.Value,newValue))
				onChange(r.Value=newValue!);
		},type,method,args);
		return r;
	}

	public static ReferenceTo<T> ListenOnChange<T>(Action<T> onChange,T @default,string type,string method,params object?[] args){
		var r=new ReferenceTo<T>();
		AutoRecall(
			msg=>{
				var newValue=msg[0].To<T>();
				if(!EqualityComparer<T?>.Default.Equals(r.Value,newValue))
					onChange(r.Value=newValue!);
			},
			_=>{
				if(!EqualityComparer<T>.Default.Equals(r.Value,@default))
					onChange(r.Value=@default);
			},
			type,method,args);
		return r;
	}
	#endregion

	#region Clone
	//Deep clones an IRpcDataObject
	public static T Clone<T>(this T t) where T : IRpcDataObject{
		var instance=Activator.CreateInstance<T>();
		if(!instance.TrySetProps(t.GetProps(new Dictionary<object,RpcDataPrimitive>()),true))
			throw new RpcDataException("Error cloning "+t);
		return instance;
	}
	#endregion
}