using System.Net;
using System.Text;
using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;
using PlayifyUtility.Web;
using PlayifyUtility.Web.Utils;
#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace PlayifyRpc;

public class RpcWebServer:WebBase{
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
		var server=new RpcWebServer(rpcJs,rpcToken);
		var task=server.RunHttp(endPoint);

		_=ServerConnectionLoopbackClient.Connect().Catch(Rpc.Logger.Error);

		await task;
	}

	private static readonly HttpClient HttpClient=new(new HttpClientHandler{UseCookies=false});

	[PublicAPI]
	public static Task<(bool success,string result)> CallDirectly(string call,bool? pretty=true)
		=>CallDirectly(call,Environment.GetEnvironmentVariable("RPC_URL")??throw new ArgumentException("Environment variable RPC_URL is not defined"),Environment.GetEnvironmentVariable("RPC_TOKEN"),pretty);

	[PublicAPI]
	public static async Task<(bool success,string result)> CallDirectly(string call,string url,string? token,bool? pretty=true){
		UriBuilder builder;
		try{
			builder=new UriBuilder(url);
			builder.Scheme=builder.Scheme switch{
				"ws"=>"http",
				"wss"=>"https",
				var s=>s,
			};
		} catch(UriFormatException){
			var endPoint=url switch{
				_ when Parsers.TryParseIpEndPoint(url,DefaultPort,out var ep)=>ep,
				_ when int.TryParse(url,out var port)=>new IPEndPoint(IPAddress.Any,port),
				_ when IPAddress.TryParse(url,out var address)=>new IPEndPoint(address,DefaultPort),
				_=>throw new CloseException($"Invalid URL, IP or Port: \"{url}\""),
			};
			builder=new UriBuilder("http",endPoint.Address.ToString(),endPoint.Port,"/rpc");
		}
		if(!pretty.TryGet(out var prettyActual)) builder.Path+="/void";
		else if(prettyActual) builder.Path+="/pretty";

		var response=await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post,builder.Uri){
			Headers={
				{"Cookie","RPC_TOKEN="+token},
			},
			Content=new StringContent(call,Encoding.UTF8,"text/plain"),
		});
		return (response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
	}

	private const string HelpText="""
	                              Commands:
	                                ./rpc.sh update
	                                ./rpc.sh <flags> listen <port/ip>
	                                ./rpc.sh <flags> call <call>
	                                ./rpc.sh help
	                              Flags:
	                                General:
	                                  -h | --help                           | Shows this help text
	                                  -s | --secure <token>   env:RPC_TOKEN | Secure connection with a token
	                                  -i | --insecure                       | Insecure mode without warning
	                                Listen:
	                                  -l | --listen <port/ip>               | Run as server others can connect to (default: 4590)
	                                  -j | --js <path>                      | rpc.js file, that is used when listening for web requests
	                                Call:
	                                  -u | --url <url>          env:RPC_URL | Connection url, e.g. ws://localhost:4590/rpc
	                                  -f | --format <pretty|compact|void>   | Configure formatting of the result (default: pretty)
	                                  -c | --call <call>                    | Call remote function
	                                  -- | call <rest...>                   | Call remote function (joins all remaining arguments)
	                              """;
	private const int DefaultPort=4590;

	internal static async Task Main(string[] args){
		var setToken=false;
		var url=Environment.GetEnvironmentVariable("RPC_URL");
		var token=Environment.GetEnvironmentVariable("RPC_TOKEN");
		var js="rpc.js";
		var listen=new List<IPEndPoint>();
		var calls=new List<string>();
		bool? pretty=true;

		try{
			for(var i=0;i<args.Length;i++){
				switch(args[i]){
					case "-u":
					case "--url":
						if(i+1==args.Length) throw new CloseException("--url requires a value");
						if(url!=null) throw new CloseException("--url already set");
						url=args[++i];
						break;
					case "-s":
					case "--secure":
						if(i+1==args.Length) throw new CloseException("--secure requires a value");
						if(setToken) throw new CloseException(token!=null?"--secure already set":"--secure is incompatible with --insecure");
						setToken=true;
						token=args[++i];
						break;
					case "-i":
					case "--insecure":
						if(setToken) throw new CloseException(token!=null?"--insecure is incompatible with --secure":"--insecure already set");
						setToken=true;
						token=null;
						break;
					case "-j":
					case "--js":
						if(i+1==args.Length) throw new CloseException("--js requires a file path");
						js=args[++i];
						break;
					case "-h":
					case "--help":
					case "help":
						throw new CloseException("");
					case "-l":
					case "--listen":
					case "listen":
						if(i+1==args.Length)
							listen.Add(new IPEndPoint(IPAddress.Any,DefaultPort));/*
							if(args[i]=="listen") listen.Add(new IPEndPoint(IPAddress.Any,DefaultPort));
							else throw new CloseException("--listen requires a value");*/
						else
							listen.Add(args[++i] switch{
								var s when int.TryParse(s,out var port)=>new IPEndPoint(IPAddress.Any,port),
								var s when IPAddress.TryParse(s,out var address)=>new IPEndPoint(address,DefaultPort),
								var s when Parsers.TryParseIpEndPoint(s,DefaultPort,out var ep)=>ep,
								var s=>throw new CloseException($"Invalid IP or Port: \"{s}\""),
							});
						break;
					case "-f":
					case "--format":
						const string options="pretty/compact/void";
						if(i+1==args.Length) throw new CloseException($"--format requires one of {options}");
						pretty=args[++i].ToLowerInvariant() switch{
							"pretty"=>true,
							"compact"=>false,
							"void"=>null,
							var s=>throw new CloseException($"Invalid value: {s}, nees to be one of {options}"),
						};
						break;
					case "-c":
					case "--call":
						if(i+1==args.Length) throw new CloseException("--call requires a value");
						calls.Add(args[++i]);
						break;
					case "--":
					case "call":
						if(i+1==args.Length) throw new CloseException("call requires a value");
						i++;
						var result=args[i++];
						while(i!=args.Length) result+=args[i++];
						calls.Add(result);
						break;
					case var unknown:
						if(args.Length==1&&args[0] switch{
							   var s when int.TryParse(s,out var port)=>new IPEndPoint(IPAddress.Any,port),
							   var s when IPAddress.TryParse(s,out var address)=>new IPEndPoint(address,DefaultPort),
							   var s when Parsers.TryParseIpEndPoint(s,DefaultPort,out var ep)=>ep,
							   _=>null,
						   } is{} foundEndpoint){
							listen.Add(foundEndpoint);
							break;
						}

						throw new CloseException($"Unknown argument: {unknown}");
				}
			}
			if(listen.Count==0&&calls.Count==0) throw new CloseException("");

			if(!setToken&&token==null) Rpc.Logger.Warning("RPC_TOKEN is not defined, connections will not be secure");

			if(listen.Count!=0) RunConsoleThread();
			if(calls.Count!=0&&url==null) throw new CloseException("RPC_URL is not defined, cannot call remote function"+(calls.Count==0?"":"s"));


		} catch(CloseException e){
			if(e.Message=="")
				await Console.Out.WriteLineAsync(PlatformUtils.IsWindows()?HelpText.Replace("./rpc.sh","rpc"):HelpText);
			else await Console.Error.WriteLineAsync(e.Message);
			return;
		}

		await Task.WhenAll(EnumerableUtils.Concat(
			listen.Select(async ep=>{
				try{
					Rpc.Logger.Info("Listening on "+ep);
					await RunWebServer(ep,js,token);
				} catch(Exception e){
					Rpc.Logger.Critical(e);
					Environment.Exit(-1);
				}
			}),
			calls.Select(async call=>{
				try{
					var (success,result)=await CallDirectly(call,url!,token,pretty);
					await (success?Console.Out:Console.Error).WriteLineAsync(result);
				} catch(Exception e){
					await Console.Error.WriteLineAsync(e.ToString());
				}
			})
		));
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
				             .Text(e.ToString(),500);
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

		if(rawUrl.StartsWith("/rpc/")||rawUrl=="/rpc"&&session.Type is RequestType.Post or RequestType.Put){
			var s=Uri.UnescapeDataString(rawUrl.Substring("/rpc".Length));


			var voidResponse=false;
			var prettyResponse=false;
			while(true)
				if("/void".RemoveFromEndOf(ref s)) voidResponse=true;
				else if("/pretty".RemoveFromEndOf(ref s)) prettyResponse=true;
				else break;

			var postArgs=session.Type is RequestType.Post or RequestType.Put?await session.ReadStringAsync():null;

			if(s.StartsWith("/")) s=s.Substring(1);

			RpcDataPrimitive result;
			try{
				result=await Evaluate.EvalObject(s,postArgs);
			} catch(Exception e){
				await session.Send
				             .Cache(false)
				             .Text(e.ToString(),500);
				return;
			}
			if(voidResponse)
				await session.Send.NoContent();
			else{
				try{
					s=result.ToString(prettyResponse);
				} catch(Exception e){
					await session.Send
					             .Cache(false)
					             .Text(e.ToString(),500);
					return;
				}
				await session.Send
				             .Cache(false)
				             .Json(s);
			}
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