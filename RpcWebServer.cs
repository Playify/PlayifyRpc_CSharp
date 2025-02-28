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

			string? postArgs=null;
			Func<Task<string>>? postArgsProvider=session.Type is RequestType.Post or RequestType.Put?async ()=>postArgs??=await session.ReadStringAsync():null;


			Task<RpcDataPrimitive>? pendingCall=null;


			var prettyResponse=false;
			(string? name,bool download) responseType=(null,true);
			Exception? lastError=null;


			s="/"+s.TrimStart('/');
			for(var slashPos=s.Length;slashPos!=-1;slashPos=s.LastIndexOf('/',slashPos-1)){
				var expression=slashPos==0?"":s.Substring(1,slashPos-1);

				//Reset options to original values
				prettyResponse=false;
				responseType=(null,true);

				if(slashPos!=s.Length){
					var optionsSuccessful=true;
					foreach(var option in s.Substring(slashPos+1).Split('/'))
						if(option==""){
						} else if(option=="void") responseType=(null,false);
						else if(option=="pretty") prettyResponse=true;
						else if(option.TryRemoveFromStartOf("file=",out var rest)) responseType=(rest,false);
						else if(option.TryRemoveFromStartOf("download=",out rest)) responseType=(rest,true);
						else{
							optionsSuccessful=false;
							break;
						}
					if(!optionsSuccessful) break;
				}


				try{
					pendingCall=await Evaluate.Eval(expression,postArgsProvider,true);
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
				} else pendingCall=await Evaluate.Eval(s.TrimStart('/'),postArgsProvider,true);

			RpcDataPrimitive result;
			try{
				result=await pendingCall;
			} catch(Exception e){
				await session.Send
				             .Cache(false)
				             .Text(e.ToString(),500);
				return;
			}

			if(responseType.name!=null){
				session.Send
				       .Cache(false)
				       .Header("Content-Disposition",$"{(responseType.download?"attachment":"inline")}; filename=\"{responseType.name.Replace("\"","\\\"")}\";");

				//var mimeType=WebUtils.MimeType(Path.GetExtension(responseType.name));
				var mimeType=MimeMapping.GetValueOrDefault(Path.GetExtension(responseType.name),"application/octet-stream");

				if(result.TryTo(out byte[]? bytes))
					await session.Send.Data(bytes!,mimeType);
				else if(result.IsString(out var str))
					await session.Send.Text(str,mimeType+"; charset=utf-8");
				else
					await session.Send.Text(result.ToString(prettyResponse),mimeType+"; charset=utf-8");
			} else if(!responseType.download)
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