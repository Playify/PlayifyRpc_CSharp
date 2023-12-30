using System.Diagnostics;
using JetBrains.Annotations;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc;

public partial class RpcWebServer{
	[PublicAPI]
	public static ValueTask ReDownloadRpcJsTo(string path){
		var task=DownloadRpcJsTo(path,true);
		if(!File.Exists(path)) return new ValueTask(task.TryCatch());//impacts startup time, but as web clients depend on rpc.js, it's the only way
		_=task.TryCatch();//don't await, it would impact startup time
		return default;
	}

	[PublicAPI]
	public static async Task DownloadRpcJsTo(string target,bool forceOverwrite){
		if(!forceOverwrite&&File.Exists(target)) return;

		var newPath=await DownloadRpcJsFromNpm();
		File.Copy(newPath,target,true);
		File.Copy(newPath+".map",target+".map",true);
	}

	[PublicAPI]
	public static async Task<string> DownloadRpcJsFromNpm(){
		var dir=Path.Combine(Path.GetTempPath(),"playify-rpc");
		Directory.CreateDirectory(dir);

		var packageJson=Path.Combine(dir,"package.json");
		const string content="{\"dependencies\": {\"playify-rpc\": \"latest\"}}";
#if NETFRAMEWORK
		using(var writer=new StreamWriter(packageJson)) await writer.WriteAsync(content);
#else
		await File.WriteAllTextAsync(packageJson,content);
#endif
		await Process.Start(PlatformUtils.IsLinux()
			                    ?new ProcessStartInfo{
				                    FileName="bash",
				                    Arguments="-c \"npm install\"",
				                    WorkingDirectory=dir,
				                    UseShellExecute=false,
			                    }
			                    :new ProcessStartInfo{
				                    FileName="cmd.exe",
				                    Arguments="/c npm install",
				                    WorkingDirectory=dir,
				                    UseShellExecute=false,
			                    })!.WaitForExitAsync();

		return Path.GetFullPath(Path.Combine(dir,"node_modules/playify-rpc/dist/rpc.js"));
	}
}