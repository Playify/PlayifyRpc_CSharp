using System.Diagnostics;
using JetBrains.Annotations;
using PlayifyUtility.Utils;

namespace PlayifyRpc;

public partial class RpcWebServer{
	[PublicAPI]
	public static async Task<string> TryDownloadRpcJs(){
		const string target="rpc.js";
		if(File.Exists(target)) return target;

		var newPath=await DownloadNewestRpcJs();
		File.Copy(newPath,target);
		File.Copy(newPath+".map",target+".map");
		return target;
	}

	[PublicAPI]
	public static async Task<string> DownloadNewestRpcJs(){
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