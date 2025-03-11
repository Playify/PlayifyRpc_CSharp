using System.Net;
using JetBrains.Annotations;
using PlayifyRpc.Connections;
using PlayifyRpc.Internal;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils.Extensions;
using PlayifyUtility.Web;
using PlayifyUtility.Web.Utils;

namespace PlayifyRpc;

public partial class RpcWebServer:WebBase{
	public override bool HandleIllegalRequests=>false;

	private readonly string _rpcJs;
	private readonly string? _rpcToken;

	private RpcWebServer(string rpcJs,string? rpcToken){
		_rpcJs=rpcJs;
		_rpcToken=rpcToken;
	}


	[PublicAPI]
	public static async Task RunWebServer(IPEndPoint endPoint,string rpcJs="rpc.js",string? rpcToken=null){
		var server=new RpcWebServer(rpcJs,rpcToken);
		var task=server.RunHttp(endPoint);
		_=ServerConnectionLoopbackClient.Connect().Catch(Rpc.Logger.Error);
		await task;
	}

	protected override Task HandleRequest(WebSession session)=>HandleRequest(session,_rpcJs,_rpcToken);

	[PublicAPI]
	public static async Task HandleRequest(WebSession session,string rpcJs,string? rpcToken){
		//Handle token
		if(!string.IsNullOrEmpty(rpcToken)){
			var s=session.Cookies.Get("RPC_TOKEN")??"";
			if(s!=rpcToken){
				await session.Send.Error(403);
				return;
			}
		}

		//Handle Websocket
		if(session.WantsWebSocket(out var create)){
			await HandleWebSocket(session,create);
			return;
		}

		var rawUrl=session.RawUrl;
		if(rawUrl.EndsWith("/")) rawUrl=rawUrl.TrimEnd('/');

		if(rawUrl.StartsWith("/rpc/")||rawUrl=="/rpc"&&session.Type is RequestType.Post or RequestType.Put){
			var s=Uri.UnescapeDataString(rawUrl.Substring("/rpc".Length));
			await HandleWebCall(session,s);
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

	private enum ResponseType{
		Default,
		Void,
		File,
		Download,
		Http,
	}

	private static async Task HandleWebCall(WebSession session,string s){
		string? postArgs=null;
		Func<Task<string>>? postArgsProvider=session.Type is RequestType.Post or RequestType.Put
			                                     ?async ()=>{//TODO test if working
				                                     if(postArgs!=null) return postArgs;
				                                     postArgs=await session.ReadStringAsync();
				                                     if(session.Headers.TryGetValue("Content-Type",out var contentType)
				                                        &&contentType.Contains("application/x-www-form-urlencoded"))
					                                     postArgs=RpcDataPrimitive.From(WebUtils.ParseQueryString(postArgs)).ToString();
				                                     return postArgs;
			                                     }
			                                     :null;


		Task<RpcDataPrimitive>? pendingCall=null;


		var prettyResponse=false;
		(ResponseType type,string? name) responseType=(ResponseType.Default,null);
		Exception? lastError=null;
		List<RpcDataPrimitive> appendArgs=[];
		RpcDataPrimitive? headersPrimitive=null;
		RpcDataPrimitive? cookiesPrimitive=null;


		s="/"+s.TrimStart('/');
		for(var slashPos=s.Length;slashPos!=-1;slashPos=s.LastIndexOf('/',slashPos-1)){
			var expression=slashPos==0?"":s.Substring(1,slashPos-1);

			//Reset options to original values
			prettyResponse=false;
			responseType=(ResponseType.Default,null);
			appendArgs.Clear();

			if(slashPos!=s.Length){
				var optionsSuccessful=true;
				foreach(var option in s.Substring(slashPos+1).Split('/'))
					if(option==""){
					} else if(option=="void") responseType=(ResponseType.Void,null);
					else if(option=="pretty") prettyResponse=true;
					else if(option=="headers") appendArgs.Add(headersPrimitive??=RpcDataPrimitive.From(session.Headers));
					else if(option=="cookies") appendArgs.Add(cookiesPrimitive??=RpcDataPrimitive.From(session.Cookies));
					else if(option=="http") responseType=(ResponseType.Http,null);
					else if(option.TryRemoveFromStartOf("file=",out var rest)) responseType=(ResponseType.File,rest);
					else if(option.TryRemoveFromStartOf("download=",out rest)) responseType=(ResponseType.Download,rest);
					else{
						optionsSuccessful=false;
						break;
					}
				if(!optionsSuccessful) break;
			}


			try{
				pendingCall=await Evaluate.Eval(expression,postArgsProvider,true,appendArgs);
				break;
			} catch(Exception e){
				lastError=e;
			}
		}
		if(pendingCall==null)
			if(lastError!=null){
				await session.Send
				             .Cache(false)
				             .Text(lastError.ToString(),500);
				return;
			} else pendingCall=await Evaluate.Eval(s.TrimStart('/'),postArgsProvider,true,appendArgs);

		RpcDataPrimitive result;
		try{
			result=await pendingCall;
		} catch(Exception e){
			await session.Send
			             .Cache(false)
			             .Text(e.ToString(),500);
			return;
		}

		switch(responseType.type){
			default:
			case ResponseType.Default:
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
				return;
			case ResponseType.Void:
				await session.Send.NoContent();
				return;
			case ResponseType.File:
			case ResponseType.Download:{
				session.Send
				       .Cache(false)
				       .Header("Content-Disposition",(responseType.type==ResponseType.Download?"attachment":"inline")
				                                     +$"; filename=\"{responseType.name?.Replace("\"","\\\"")}\";");

				var mimeType=MimeMapping.GetValueOrDefault(Path.GetExtension(responseType.name??""),"application/octet-stream");


				if(result.TryTo(out byte[]? bytes))
					await session.Send.Data(bytes!,mimeType);
				else if(result.IsString(out var str))
					await session.Send.Text(str,mimeType+"; charset=utf-8");
				else
					await session.Send.Text(result.ToString(prettyResponse),mimeType+"; charset=utf-8");
				return;
			}
			case ResponseType.Http:{
				session.Send.Cache(false);

				if(result.IsObject(out var entries)){
					var response=entries.ToDictionary();
					var status=response.TryGetValue("status",out var statusPrimitive)
						           ?statusPrimitive.TryTo(out int statusNumber)
							            ?statusNumber
							            :500
						           :200;

					if(response.TryGetValue("headers",out var responseHeaders))
						if(responseHeaders.IsObject(out var headersObject)){
							foreach(var (key,value) in headersObject)
								session.Send.Header(key,value.IsString(out var headerValue)?headerValue:value.ToString());
						} else if(responseHeaders.IsString(out var headersString)){
							foreach(var headerLine in headersString.Split('\r','\n'))
								if(headerLine.SliceAt(':') is{} tuple)//Check valid header
									session.Send.Header(tuple.left,tuple.right);
						} else if(responseHeaders.IsArray(out var headersArray)){
							foreach(var headerLinePrimitive in headersArray)
								if(headerLinePrimitive.IsString(out var headerLine)
								   &&headerLine.SliceAt(':') is{} tuple)//Check valid header
									session.Send.Header(tuple.left,tuple.right);
						}//else, invalid header => ignore


					if(!response.TryGetValue("body",out var responseBody))
						await session.Send.Data([],null,status);
					else if(responseBody.TryTo(out byte[]? bytes))
						await session.Send.Data(bytes!,null,status);
					else if(responseBody.IsString(out var str))
						await session.Send.Text(str,null,status);
					else
						await session.Send.Text(responseBody.ToString(prettyResponse),null,status);

				} else if(result.TryTo(out byte[]? bytes))
					await session.Send.Data(bytes!,null);
				else if(result.IsString(out var str))
					await session.Send.Text(str,null);
				else
					await session.Send.Text(result.ToString(prettyResponse),null);
				return;
			}
		}
	}

	private static async Task HandleWebSocket(WebSession session,Func<Task<WebSocket>> create){
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
	}
}