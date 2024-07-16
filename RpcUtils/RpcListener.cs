using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.RpcUtils;

[PublicAPI]
public static class RpcListener{
	public static RpcListener<T?> CreateNull<T>(string type="",string method="")=>new(default,type,method);
	public static RpcListener<T?> CreateConst<T>(T t,string type="",string method="")=>new(t,type,method);

	public static RpcListener<T?> Create<T>(string type,string method="listen")
		=>new(type,
		      method,
		      t=>DynamicCaster.Cast<T>(t[0]),
		      _=>default);

	public static RpcListener<T?> Create<T>(string type,Action<T?> onChange)=>Create(type,"listen",onChange);

	public static RpcListener<T?> Create<T>(string type,string method,Action<T?> onChange){
		var listener=Create<T>(type,method);
		listener.OnChange+=onChange;
		return listener;
	}
}

[PublicAPI]
public class RpcListener<T>{
	public readonly string Type;
	public readonly string Method;
	private T _value;

	public T Value{
		get=>_value;
		private set{
			if(EqualityComparer<T>.Default.Equals(_value,value)) return;
			_value=value;
			OnChange(value);
		}
	}

	public event Action<T> OnChange=delegate{};


	public RpcListener(T t,string type="",string method=""){
		Type=type;
		Method=method;

		_value=t;
	}

	public RpcListener(string type,string method,Func<object?[],T> messageConverter,Func<Exception?,T> disconnect){
		Type=type;
		Method=method;

		_value=disconnect(null);

		Run().Background();
		return;

		async Task Run(){
			var connected=false;
			while(true)
				try{
					await Rpc.WaitUntilConnected;

					var pendingCall=Rpc.CallFunction(Type,Method);
					pendingCall.AddMessageListener(args=>{
						connected=true;
						Value=messageConverter(args);
					});
					await pendingCall;
				} catch(Exception e){
					if(connected){
						connected=false;
						Value=disconnect(e);
					}
				} finally{
					if(connected){
						connected=false;
						Value=disconnect(null);
					}
					await Task.Delay(TimeSpan.FromSeconds(1));
				}
		}
	}
}