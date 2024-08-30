using PlayifyRpc.Connections;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

internal class ServerInvoker:Invoker{
	private readonly ServerConnection _connection;
	private readonly List<(string name,int? args,Delegate @delegate)> _methods;

	public ServerInvoker(ServerConnection connection){
		_connection=connection;
		_methods=[
			("N",null,Name),
			("H",1,(Action<string>)Handshake),//Connections
			("H",2,(Action<string[],string[]>)Handshake),//Connections
			("H",3,(Action<string,string[],string[]>)Handshake),//Connections
			("+",null,Register),//Rpc.RegisterType
			("-",null,Unregister),//Rpc.UnregisterType

			("E",null,RpcServer.CheckType),//RpcObject.Exists
			("c",null,_connection.GetCaller),//FunctionCallContext.GetCaller
		];
	}


	private void Name(string? name)=>_connection.Name=name;

	private void Handshake(string? name)=>Name(name);

	private void Handshake(string? name,string[]? register,string[]? unregister){
		Name(name);
		Handshake(register,unregister);
	}

	private void Handshake(string[]? register,string[]? unregister){
		if(register!=null) _connection.Register(register,false);
		if(unregister!=null) _connection.Unregister(unregister,false);
	}

	private void Register(params string[] types)=>_connection.Register(types,true);

	private void Unregister(params string[] types)=>_connection.Unregister(types,true);


	protected override object? DynamicInvoke(string? type,string method,object?[] args){
		foreach(var (name,i,@delegate) in _methods)
			if(method.Equals(name,StringComparison.OrdinalIgnoreCase)
			   &&(!i.TryGet(out var argCount)||argCount==args.Length))
				return DynamicBinder.Invoke(@delegate,type,method,args);
		throw new RpcMethodNotFoundException(type,method);
	}

	protected override ValueTask<string[]> GetMethods()=>new(_methods.Select(t=>t.name).Distinct().Ordered().ToArray());

	protected override ValueTask<(string[] parameters,string returns)[]> GetMethodSignatures(string? type,string method,bool ts){
		var signatures=_methods
		               .Where(t=>t.name.Equals(method,StringComparison.OrdinalIgnoreCase))
		               .SelectMany(t=>DynamicTypeStringifier.MethodSignatures(t.@delegate,ts))
		               .ToArray();
		return signatures.Length==0
			       ?new ValueTask<(string[] parameters,string returns)[]>(Task.FromException<(string[] parameters,string returns)[]>(new RpcMethodNotFoundException(type,method)))
			       :new ValueTask<(string[] parameters,string returns)[]>(signatures);
	}
}