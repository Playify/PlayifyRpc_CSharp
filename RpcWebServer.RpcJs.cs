using System.Diagnostics;
using System.Net;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;
#if NET48
using PlayifyUtility.Utils.Extensions;
#endif

namespace PlayifyRpc;

public partial class RpcWebServer{
	public static async Task DownloadRpcJs(bool overwrite){
		if(File.Exists("rpc.js")&&!overwrite) return;


		string tarballFile;
#pragma warning disable SYSLIB0014
		using(var client=new WebClient()){
#pragma warning restore SYSLIB0014
			var jsonString=await client.DownloadStringTaskAsync("https://registry.npmjs.org/playify-rpc/latest");
			if(!JsonObject.TryParse(jsonString,out var json))
				throw new Exception("Error parsing npm package");

			tarballFile=Path.GetTempFileName();
			await client.DownloadFileTaskAsync(json["dist"]["tarball"].AsString(),tarballFile);
		}

		using var process=Process.Start(new ProcessStartInfo{
			FileName="tar",
			Arguments=CommandLineUtils.EscapeArguments(
				"-xzf",
				tarballFile,
				"--strip-components=2",
				"-C",
				".",
				"package/dist/"
			),
			UseShellExecute=false,
			RedirectStandardOutput=true,
			RedirectStandardError=true,
			WorkingDirectory=Environment.CurrentDirectory,
		})!;
		await process.WaitForExitAsync();

		var error=await process.StandardError.ReadToEndAsync();
		if(!string.IsNullOrEmpty(error))
			throw new Exception("Error extracting tarball: "+error);

		File.Delete(tarballFile);

		if(!File.Exists("rpc.js"))
			throw new Exception("Something went wrong while getting rpc.js");
	}
}