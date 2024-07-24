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
					Console.WriteLine("Commands:"+
					                  "\n\tE/X/Q : exit"+
					                  "\n\t    C : list connections"+
					                  "\n\t    T : list types"+
					                  "\n\t    R : list registrations");
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
		if(rpcToken==null) Rpc.Logger.Warning("RPC_TOKEN is not defined, connections will not be secure");
		var server=new RpcWebServer(rpcJs,rpcToken);
		var task=server.RunHttp(endPoint);

		Rpc.ConnectLoopback();

		await task;
	}

	internal static async Task Main(string[] args){
		if(args.Length!=0&&args[0]=="help"){
			Console.WriteLine("use args: [IP:Port or Port] [rpcToken] [rpc.js path]\n"+
			                  "default port: 4590\n"+
			                  "rpcToken will be used from RPC_TOKEN environment variable\n"+
			                  "rpc.js path if omitted will download when the file doesn't exist");
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

		var rpcToken=args.Length>1?args[1]:Environment.GetEnvironmentVariable("RPC_TOKEN")??null;

		var rpcJs="rpc.js";
		if(args.Length>2) rpcJs=args[2];
		else if(!File.Exists("rpc.js")) DownloadRpcJs().Background();

		RunConsoleThread();
		try{
			Rpc.Logger.Info("Listening on "+ipEndPoint);
			await RunWebServer(ipEndPoint,rpcJs,rpcToken);
		} catch(Exception e){
			Rpc.Logger.Critical(e);
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


		if(session.WantsWebSocket(out var create)){
			ServerConnectionWebSocket connection;
			try{
				connection=new ServerConnectionWebSocket(create,session.Args);
			} catch(Exception e){
				await session.Send
				             .Cache(false)
				             .Text(e.ToString(),code:500);
				return;
			}
			try{
				await connection.WebSocketTask;
			} catch(Exception){
				await connection.DisposeAsync();
				return;
			}
			try{

				string types;
				lock(RpcServer.Types) types=connection.Types.Where(t=>t!="$"+connection.Id).Select(t=>$"\"{t}\"").Join(",");

				connection.Logger.Info(types==""
					                       ?"Connected (no Types)"
					                       :$"Connected (Types: {(types!=""?types:"<<none>>")})");

				await connection.ReceiveLoop();
			} finally{
				connection.Logger.Info("Disconnected");

				await connection.DisposeAsync();
			}
			return;
		}
		var rawUrl=session.RawUrl;
		if(rawUrl.EndsWith("/")) rawUrl=rawUrl.TrimEnd('/');

		if(rawUrl.StartsWith("/rpc/")){
			var s=Uri.UnescapeDataString(rawUrl.Substring("/rpc/".Length));


			var voidResponse=false;
			var prettyResponse=false;
			while(true)
				if("/void".RemoveFromEndOf(ref s)) voidResponse=true;
				else if("/pretty".RemoveFromEndOf(ref s)) prettyResponse=true;
				else break;

			try{
				s=await Evaluate.EvalString(s,prettyResponse);
			} catch(Exception e){
				await session.Send
				             .Cache(false)
				             .Text(e.ToString(),code:500);
				return;
			}
			if(voidResponse)
				await session.Send.NoContent();
			else
				await session.Send
				             .Cache(false)
				             .Json(s);
			return;
		}


		switch(session.Path){
			case "/rpc.js":
				await session.Send.File(Path.ChangeExtension(rpcJs,".js"));
				return;
			case "/rpc.js.map":
				await session.Send.File(Path.ChangeExtension(rpcJs,".js.map"));
				return;
			case "/":
			case "/rpc":
			case "/rpc.html":
				await session.Send.File(Path.ChangeExtension(rpcJs,".html"));
				return;
			default:
				await session.Send.Error(404);
				return;
		}
	}
}