using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.HelperClasses;

namespace PlayifyRpc.Utils;

[PublicAPI]
public static class RpcHelpers{

	#region AutoRecall
	public static void AutoRecall(Delegate onMessage,string type,string method,params object?[] args)=>AutoRecall(onMessage,null,type,method,args);
	public static void AutoRecall(Delegate onMessage,TimeSpan retryDelay,string type,string method,params object?[] args)=>AutoRecall(onMessage,null,retryDelay,type,method,args);
	public static void AutoRecall(Delegate onMessage,Action<RpcException?>? onError,string type,string method,params object?[] args)=>AutoRecall(onMessage,onError,TimeSpan.FromSeconds(1),type,method,args);

	public static async void AutoRecall(Delegate onMessage,Action<RpcException?>? onError,TimeSpan retryDelay,string type,string method,params object?[] args){
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

	public static void AutoRecallRawMsg(Action<RpcDataPrimitive[]> onMessage,string type,string method,params object?[] args)=>AutoRecall(onMessage,null,type,method,args);
	public static void AutoRecallRawMsg(Action<RpcDataPrimitive[]> onMessage,TimeSpan retryDelay,string type,string method,params object?[] args)=>AutoRecall(onMessage,null,retryDelay,type,method,args);
	public static void AutoRecallRawMsg(Action<RpcDataPrimitive[]> onMessage,Action<RpcException?>? onError,string type,string method,params object?[] args)=>AutoRecall(onMessage,onError,TimeSpan.FromSeconds(1),type,method,args);

	public static async void AutoRecallRawMsg(Action<RpcDataPrimitive[]> onMessage,Action<RpcException?>? onError,TimeSpan retryDelay,string type,string method,params object?[] args){
		while(true){
			RpcException? ex;
			try{
				var call=Rpc.CallFunction(type,method,args);
				_=call.AddMessageListenerRaw(onMessage);
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
		AutoRecall((T msg)=>r.Value=msg,type,method,args);
		return r;
	}

	public static ReferenceTo<T> ListenValue<T>(T @default,string type,string method,params object?[] args){
		var r=new ReferenceTo<T>(@default);
		AutoRecall((T msg)=>r.Value=msg,_=>r.Value=@default,type,method,args);
		return r;
	}
	#endregion

	#region ListenOnChange
	public static ReferenceTo<T> ListenOnChange<T>(Action<T> onChange,string type,string method,params object?[] args){
		var r=new ReferenceTo<T>();
		AutoRecall((T newValue)=>{
			if(!EqualityComparer<T?>.Default.Equals(r.Value,newValue))
				onChange(r.Value=newValue!);
		},type,method,args);
		return r;
	}

	public static ReferenceTo<T> ListenOnChange<T>(Action<T> onChange,T disconnectValue,string type,string method,params object?[] args){
		var r=new ReferenceTo<T>();
		AutoRecall((T newValue)=>{
				if(!EqualityComparer<T?>.Default.Equals(r.Value,newValue))
					onChange(r.Value=newValue!);
			},
			_=>{
				if(!EqualityComparer<T>.Default.Equals(r.Value,disconnectValue))
					onChange(r.Value=disconnectValue);
			},
			type,method,args);
		return r;
	}
	#endregion

	#region Clone
	//Deep clones an IRpcDataObject
	public static T Clone<T>(this T t) where T : IRpcDataObject{
		var instance=Activator.CreateInstance<T>();
		var already=new RpcDataPrimitive.Already(a=>a());//Should never be needed to dispose, but just in case, don't leave any waste behind
		if(!instance.TrySetProps(t.GetProps(already,null),true,null,new RpcDataPrimitive()))
			throw new RpcDataException("Error cloning "+t);
		return instance;
	}
	#endregion

}