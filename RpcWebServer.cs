using System.Net;
using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;
using PlayifyUtility.Web;

namespace PlayifyRpc;

public partial class RpcWebServer:WebBase{
	public override bool HandleIllegalRequests=>false;

	private readonly string _rpcJs;
	private readonly string? _rpcToken;

	private RpcWebServer(string rpcJs,string? rpcToken){
		_rpcJs=rpcJs;
		_rpcToken=rpcToken;
	}

	private static void ConsoleThread(){
		while(true){
			var key=Console.ReadKey(true);

			switch(key.Key){
				case ConsoleKey.E:
				case ConsoleKey.X:
				case ConsoleKey.Q:
					Environment.Exit(0);
					return;
				case ConsoleKey.C:
					Console.WriteLine("Connections: "+RpcServer.GetAllConnections().Select(s=>"\n\t"+s).Join(""));
					break;
				case ConsoleKey.T:
					Console.WriteLine("Types: "+RpcServer.GetAllTypes().Select(s=>"\n\t"+s).Join(""));
					break;
				case ConsoleKey.R:
					Console.WriteLine("Registrations: "+RpcServer.GetRegistrations()
					                                             .OrderBy(p=>p.Key)
					                                             .Select(pair=>"\n\t"+pair.Key+":"+pair.Value
					                                                                                   .OrderBy(s=>s)
					                                                                                   .Select(s=>"\n\t\t\""+s+"\"").Join("")).Join(""));
					break;
				case ConsoleKey.Enter:
				case ConsoleKey.Spacebar:
					Console.WriteLine();
					break;
				default:
					Console.WriteLine("Commands:");
					Console.WriteLine("\tE/X/Q : exit");
					Console.WriteLine("\t    C : list connections");
					Console.WriteLine("\t    T : list types");
					Console.WriteLine("\t    R : list registrations");
					break;
			}
		}
	}

	[PublicAPI]
	public static void RunConsoleThread()=>new Thread(ConsoleThread){Name="ConsoleThread"}.Start();


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
			Console.WriteLine("use args: [IP:Port or Port] [rpc.js path] [rpcToken]");
			Console.WriteLine("default port: 4590");
			Console.WriteLine("rpc.js path if omitted will download when the file doesn't exist");
			Console.WriteLine("rpcToken will be used from RPC_TOKEN environment variable");
			return;
		}
		const int defaultPort=4590;
		var ipEndPoint=args.Length!=0
			               ?args[0] switch{
				               var s when Parsers.TryParseIpEndPoint(s,defaultPort,out var ep)=>ep,
				               var s when int.TryParse(s,out var port)=>new IPEndPoint(IPAddress.Any,port),
				               var s when IPAddress.TryParse(s,out var address)=>new IPEndPoint(address,defaultPort),
				               _=>throw new ArgumentException("Invalid IP or Port"),
			               }
			               :new IPEndPoint(IPAddress.Any,defaultPort);

		var rpcJs="rpc.js";
		if(args.Length>1) rpcJs=args[1];
		else _=DownloadRpcJs(false).TryCatch();

		var rpcToken=args.Length>2?args[2]:Environment.GetEnvironmentVariable("RPC_TOKEN")??null;

		RunConsoleThread();
		try{
			Console.WriteLine("Listening on "+ipEndPoint);
			await RunWebServer(ipEndPoint,rpcJs,rpcToken);
		} catch(Exception e){
			Console.WriteLine(e);
			Environment.Exit(-1);
		}
	}

	protected override Task HandleRequest(WebSession session)=>HandleRequest(session,_rpcJs,_rpcToken);

	[PublicAPI]
	public static async Task HandleRequest(WebSession session,string rpcJs,string? rpcToken){
		if(!string.IsNullOrEmpty(rpcToken)){
			var s=session.Cookies.Get("RPC_TOKEN")??"";
			if(s!=rpcToken){
				await session.Send.Error(403);
				return;
			}
		}


		if(await session.CreateWebSocket() is{} webSocket){
			await using var connection=new ServerConnectionWebSocket(webSocket,session.Args);
			string types;
			lock(RpcServer.Types) types=connection.Types.Select(t=>$"\"{t}\"").Join(",");
			Console.WriteLine($"{connection} connected (Types: "+types+")");
			try{
				await connection.ReceiveLoop();
			} finally{
				Console.WriteLine($"{connection} disconnected");
			}
			return;
		}
		var rawUrl=session.RawUrl;
		if(rawUrl.EndsWith("/")) rawUrl=rawUrl.TrimEnd('/');

		if(rawUrl.StartsWith("/rpc/")){
			var s=Uri.UnescapeDataString(rawUrl.Substring("/rpc/".Length));

			var voidResponse=s.EndsWith("/void");
			if(voidResponse) s=s.Substring(0,s.Length-"/void".Length);

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
			if(voidResponse)
				await session.Send.Begin(204);
			else
				await session.Send
				             .Cache(false)
				             .Document()
				             .MimeType("application/json")
				             .Set(s)
				             .Send();
			return;
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