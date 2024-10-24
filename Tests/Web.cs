using System.Net;
using PlayifyRpc;
using PlayifyUtility.Utils.Extensions;

namespace Tests;

public static class Web{
	private static readonly HttpClient HttpClient=new();
	private static string _endPoint=null!;

	[SetUp]
	public static void Setup(){
		var endPoint=new IPEndPoint(new IPAddress([127,2,4,8]),4591);
		_endPoint="http://"+endPoint;
		RpcWebServer.RunWebServer(endPoint,"rpc.js",null).Background();
	}

	private static async Task<string> Request(string uri,string? postArgs)
		=>await (await (postArgs==null
			                ?HttpClient.GetAsync(_endPoint+uri)
			                :HttpClient.PostAsync(_endPoint+uri,new StringContent(postArgs))))
		        .Content.ReadAsStringAsync();

	[Test]
	public static void WebRequests()=>Assert.Multiple(async ()=>{
		Assert.That(await Request("/rpc/Rpc.Return([1])",null),Is.EqualTo("[1]"));

		Assert.That(await Request("/rpc/Rpc.Return","[1]"),Is.EqualTo("1"));
		Assert.That(await Request("/rpc/Rpc.Return()","[1]"),Is.EqualTo("[1]"));
		Assert.That(await Request("/rpc/Rpc.ReturnArguments(1)","2,3"),Is.EqualTo("[1,2,3]"));
		Assert.That(await Request("/rpc/Rpc.Return()/pretty","[1]"),Is.EqualTo("[\n\t1\n]"));
		Assert.That(await Request("/rpc","Rpc.Return([1])"),Is.EqualTo("[1]"));
		Assert.That(await Request("/rpc/pretty","Rpc.Return([1])"),Is.EqualTo("[\n\t1\n]"));
		Assert.That(await Request("/rpc","[\"Rpc\",\"Return\",[1]]"),Is.EqualTo("[1]"));
		Assert.That(await Request("/rpc/pretty","[\"Rpc\",\"Return\",[1]]"),Is.EqualTo("[\n\t1\n]"));
		Assert.That(await Request("/rpc/void","Rpc.Return([1])"),Is.EqualTo(""));
	});


	[OneTimeTearDown]
	public static void TearDown(){
		HttpClient.Dispose();
	}
}