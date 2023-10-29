using System.Net;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;
using PlayifyUtility.Web;

namespace PlayifyRpc;

internal class Program:WebBase{
	
	private static void ConsoleThread(){
		while(true){
			var line=ReadLine.Read();
			//var line=Console.ReadLine();

			switch(line){
				case "exit" or "restart" or "r" or "e":
					Environment.Exit(0);
					break;
				case "connection" or "connnections" or "con" or "c":
					Console.WriteLine("Connections: "+RpcServerTypes.GetAllConnections().Select(s=>"\n\t"+s).Join(""));
					break;
				case "type" or "types" or "t":
					Console.WriteLine("Types: "+RpcServerTypes.GetAllTypes().Select(s=>"\n\t"+s).Join(""));
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

	public static async Task Main(string[] args){
		
		new Thread(ConsoleThread){Name="ConsoleThread"}.Start();
		try{
			var server=new Program(args.Length==0?"rpc.js":args[0]);
			var task=server.RunHttp(new IPEndPoint(new IPAddress(new byte[]{127,2,4,8}),4590));
			
			Rpc.ConnectLoopback();

			await task;
		} catch(Exception e){
			Console.WriteLine(e);
			Environment.Exit(-1);
		}
	}

	private readonly string _rpcJs;
	private Program(string rpcJs){
		_rpcJs=rpcJs;
	}

	protected override async Task HandleRequest(WebSession session){
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
			}catch(Exception e){
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
				await session.Send.File(_rpcJs);
				return;
			case "/rpc.js.map":
				await session.Send.File(_rpcJs+".map");
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