using System.Diagnostics;
using JetBrains.Annotations;
using PlayifyUtility.Utils;

namespace PlayifyRpc;

public partial class RpcWebServer{
	[PublicAPI]
	public static async Task DownloadRpcJsTo(string target,bool forceOverwrite=false){
		if(!forceOverwrite&&File.Exists(target)) return;

		var newPath=await DownloadRpcJsFromNpm();
		File.Copy(newPath,target,true);
		File.Copy(newPath+".map",target+".map",true);
	}

	[PublicAPI]
	public static async Task<string> DownloadRpcJsFromNpm(){
		var dir=Path.Combine(Path.GetTempPath(),"playify-rpc");
		Directory.CreateDirectory(dir);

		await File.WriteAllTextAsync(Path.Combine(dir,"package.json"),"{\"dependencies\": {\"playify-rpc\": \"latest\"}}");
		await Process.Start(PlatformUtils.IsLinux()?new ProcessStartInfo{
			FileName="bash",
			Arguments="-c \"npm install\"",
			WorkingDirectory=dir,
			UseShellExecute=false,
		}:new ProcessStartInfo{
			FileName="cmd.exe",
			Arguments="/c npm install",
			WorkingDirectory=dir,
			UseShellExecute=false,
		})!.WaitForExitAsync();

		return Path.GetFullPath(Path.Combine(dir,"node_modules/playify-rpc/dist/rpc.js"));
	}
}