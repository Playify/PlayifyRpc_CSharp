using PlayifyRpc.Connections;
using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;

namespace PlayifyRpc.Internal;

internal class ServerInvoker:TypeInvoker{
	private readonly ServerConnection _connection;
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

	private bool Exists(string type)=>RpcServer.CheckType(type);

	[Obsolete]
	private int CheckTypes(params string[] types)=>RpcServer.CheckTypes(types);

	[Obsolete]
	private RpcObject? GetObjectWithFallback(params string[] types)=>RpcServer.GetObjectWithFallback(types);

	[Obsolete]
	private string[] GetAllTypes()=>RpcServer.GetAllTypes();

	[Obsolete]
	private string[] GetAllConnections()=>RpcServer.GetAllConnections();

	[Obsolete]
	private StringMap<string[]> GetRegistrations()=>RpcServer.GetRegistrations();

	public ServerInvoker(ServerConnection connection)=>_connection=connection;

	private static readonly Dictionary<string,string> MethodMap=new(){
		{"N",nameof(Name)},
		{"H",nameof(Handshake)},
		{"+",nameof(Register)},
		{"-",nameof(Unregister)},

		{"E",nameof(Exists)},

		{"?",nameof(CheckTypes)},
		{"O",nameof(GetObjectWithFallback)},
		{"T",nameof(GetAllTypes)},
		{"C",nameof(GetAllConnections)},
		{"R",nameof(GetRegistrations)},
	};
	protected override object? DynamicInvoke(string? type,string method,object?[] args)=>base.DynamicInvoke(type,MethodMap.TryGetValue(method,out var m)?m:method,args);

	protected override ValueTask<string[]> GetMethods()=>new(MethodMap.Keys.ToArray());
}