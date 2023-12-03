using JetBrains.Annotations;
using PlayifyRpc.Internal;
using PlayifyUtility.Utils;

namespace PlayifyRpc.RpcUtils;

[PublicAPI]
public static class RpcListener{
	public static RpcListener<T?> Create<T>(string type,string method="listen") where T:struct
		=>new(type,
		      method,
		      t=>StaticallyTypedUtils.Cast<T>(t[0]),
		      ()=>null);
	public static RpcListener<T?> Create<T>(string type,Action<T?> onChange) where T:struct=>Create(type,"listen",onChange);
	public static RpcListener<T?> Create<T>(string type,string method,Action<T?> onChange) where T:struct{
		var listener=Create<T>(type,method);
		listener.OnChange+=onChange;
		return listener;
	}
}

[PublicAPI]
public class RpcListener<T>{
	private readonly string _type;
	private readonly string _method;
	private readonly Func<object?[],T> _onMessage;
	private readonly Func<T> _onDisconnect;
	private T _value;

	public T Value{
		get=>_value;
		private set{
			if(EqualityComparer<T>.Default.Equals(_value,value)) return;
			_value=value;
			OnChange(value);
		}
	}

	public RpcListener(string type,string method,Func<object?[],T> messageConverter,Func<T> disconnect){
		_type=type;
		_method=method;
		_onMessage=messageConverter;
		_onDisconnect=disconnect;

		_value=_onDisconnect();
		Run().TryCatch();
	}

	public event Action<T> OnChange=delegate{};


	private async Task Run(){
		await Rpc.WaitUntilConnected;
		while(true){
			try{
				var pendingCall=Rpc.CallFunction(_type,_method);
				pendingCall.AddMessageListener(args=>Value=_onMessage(args));
				await pendingCall;

			} catch(Exception e){
				Console.WriteLine("Disconnected from "+nameof(RpcListener)+": "+e);
			} finally{
				_onDisconnect();
				await Task.Delay(TimeSpan.FromSeconds(1));
			}
		}
	}
}