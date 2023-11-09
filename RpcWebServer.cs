using System.Net;
using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyUtility.Utils;
using PlayifyUtility.Web;

namespace PlayifyRpc;

public partial class RpcWebServer:WebBase{

	private static void ConsoleThread(){
		while(true){
			var line=ReadLine.Read();
			//var line=Console.ReadLine();

			switch(line){
				case "exit" or "restart" or "r" or "e":
					Environment.Exit(0);
					break;
				case "connection" or "connnections" or "con" or "c":
					Console.WriteLine("Connections: "+RpcServer.GetAllConnections().Select(s=>"\n\t"+s).Join(""));
					break;
				case "type" or "types" or "t":
					Console.WriteLine("Types: "+RpcServer.GetAllTypes().Select(s=>"\n\t"+s).Join(""));
					break;/*
				case "debug" or "dbg" or "d":
					if(WebSocket.PingCountUntilError!=0){
						Console.WriteLine("Enabling debug mode");
						WebSocket.PingCountUntilError=0;
					} else{
						Console.WriteLine("Disabling debug mode");
						WebSocket.PingCountUntilError=5;
					}
					break;*/
				default:
					Console.WriteLine("Unknown Command: "+line);
					break;
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}

	[PublicAPI]
	public static void RunConsoleThread(){
		new Thread(ConsoleThread){Name="ConsoleThread"}.Start();
	}
	

	[PublicAPI]
	public static async Task RunWebServer(IPEndPoint endPoint,string rpcJs)=>await RunWebServer(endPoint,rpcJs,Environment.GetEnvironmentVariable("RPC_TOKEN"));

	[PublicAPI]
	public static async Task RunWebServer(IPEndPoint endPoint,string rpcJs,string? rpcToken){
		if(rpcToken==null) Console.WriteLine("RPC_TOKEN is not defined, connections will be not secure");
		var server=new RpcWebServer(rpcJs,rpcToken);
		var task=server.RunHttp(endPoint);

		Rpc.ConnectLoopback();

		await task;
	}

	internal static async Task Main(string[] args){
		if(args.Length!=0&&args[0]=="help"){
			Console.WriteLine("use args: <IP:Port or Port> [rpc.js path] [rpcToken]");
			Console.WriteLine("rpcToken will be used from RPC_TOKEN environment variable");
			return;
		}
		var ipEndPoint=args.Length!=0
		               ?args[0] switch{
			               var s when IPEndPoint.TryParse(s,out var ep)=>ep,
			               var s when int.TryParse(s,out var port)=>new IPEndPoint(IPAddress.Any,port),
			               _=>throw new ArgumentException("Invalid IP or Port"),
		               }
		               :new IPEndPoint(IPAddress.Any,4590);
		string rpcJs;
		if(args.Length>1) rpcJs=args[1];
		else{
			rpcJs="rpc.js";
			await DownloadRpcJsTo(rpcJs);
		}
		var rpcToken=args.Length>2?args[2]:Environment.GetEnvironmentVariable("RPC_TOKEN");

		RunConsoleThread();
		try{
			Console.WriteLine("Listening on "+ipEndPoint);
			await RunWebServer(ipEndPoint,rpcJs,rpcToken);
		} catch(Exception e){
			Console.WriteLine(e);
			Environment.Exit(-1);
		}
	}

	private readonly string _rpcJs;
	private readonly string? _rpcToken;

	private RpcWebServer(string rpcJs,string? rpcToken){
		_rpcJs=rpcJs;
		_rpcToken=rpcToken;
	}

	protected override Task HandleRequest(WebSession session)=>HandleRequest(session,_rpcJs,_rpcToken);

	[PublicAPI]
	public static async Task HandleRequest(WebSession session,string rpcJs,string? rpcToken){
		if(rpcToken!=null){
			var s=session.Cookies.Get("RPC_TOKEN");
			if(s!=rpcToken){
				await session.Send.Error(403);
				return;
			}
		}
		
		
		if(await session.CreateWebSocket() is{} webSocket){
			await using var connection=new ServerConnectionWebSocket(webSocket);
			Console.WriteLine($"{connection} connected");
			try{
				await connection.ReceiveLoop();
			} finally{
				Console.WriteLine($"{connection} disconnected");
			}
			return;
		}
		if(session.RawUrl.StartsWith("/rpc/")){
			var s=Uri.UnescapeDataString(session.RawUrl["/rpc/".Length..]);

			try{
				s=await Rpc.Eval(s);
			} catch(Exception e){
				await session.Send
				             .Cache(false)
				             .Document()
				             .Code(500)
				             .MimeType("text/plain")
				             .Set(e.ToString())
				             .Send();
				return;
			}

			await session.Send
			             .Cache(false)
			             .Document()
			             .MimeType("application/json")
			             .Set(s)
			             .Send();
		}
		switch(session.Path){
			case "/rpc.js":
				await session.Send.File(rpcJs);
				return;
			case "/rpc.js.map":
				await session.Send.File(rpcJs+".map");
				return;
			case "/":
			case "/rpc":
			case "/rpc.html":
				await session.Send.Document()
				             .Set("<title>RPC Test</title><script type=\"module\" src=\"/rpc.js\"></script>")
				             .Send();
				return;
			default:
				await session.Send.Error(404);
				return;
		}
	}
}