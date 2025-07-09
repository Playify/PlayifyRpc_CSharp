using System.Net;
using PlayifyRpc;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace Tests;

public static class Web{
	private static readonly HttpClient HttpClient=new(new HttpClientHandler{UseProxy=false});
	private static string _endPoint=null!;

	[SetUp]
	public static void Setup(){
		var endPoint=new IPEndPoint(new IPAddress([127,2,4,8]),4591);
		_endPoint="http://"+endPoint;
		RpcWebServer.RunWebServer(endPoint).Background();
	}

	private static async Task<string> Request(string uri,string? postArgs)
		=>await (await (postArgs==null
			                ?HttpClient.GetAsync(_endPoint+uri)
			                :HttpClient.PostAsync(_endPoint+uri,new StringContent(postArgs))))
		        .Content.ReadAsStringAsync();

	[Test]
	public static void WebRequests()=>Assert.Multiple(async ()=>{
		Func<Task>[] tests=[
			async ()=>Assert.That(await Request("/rpc/Rpc.Return([1])",null),Is.EqualTo("[1]")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return([1])",null),Is.EqualTo("[1]")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return(\"%5Cn\")",null),Is.EqualTo("\"\\n\"")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return(\"%5C%5Cn\")",null),Is.EqualTo("\"\\\\n\"")),

			async ()=>Assert.That(await Request("/rpc/Rpc.Return","[1]"),Is.EqualTo("1")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return()","[1]"),Is.EqualTo("[1]")),
			async ()=>Assert.That(await Request("/rpc/Rpc.ReturnArguments(1)","2,3"),Is.EqualTo("[1,2,3]")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return()/pretty","[1]"),Is.EqualTo("[\n\t1\n]")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return/pretty","[[1]]"),Is.EqualTo("[\n\t1\n]")),
			async ()=>Assert.That(await Request("/rpc","Rpc.Return([1])"),Is.EqualTo("[1]")),
			async ()=>Assert.That(await Request("/rpc/pretty","Rpc.Return([1])"),Is.EqualTo("[\n\t1\n]")),
			async ()=>Assert.That(await Request("/rpc","[\"Rpc\",\"Return\",[1]]"),Is.EqualTo("[1]")),
			async ()=>Assert.That(await Request("/rpc/pretty","[\"Rpc\",\"Return\",[1]]"),Is.EqualTo("[\n\t1\n]")),
			async ()=>Assert.That(await Request("/rpc/void","Rpc.Return([1])"),Is.EqualTo("")),

			async ()=>Assert.That(await Request("/rpc/Rpc.Return()/http","{\"body\":123}"),Is.EqualTo("123")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return()/body=binary","12"),Is.EqualTo("[49,50]")),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return()/body=string","12"),Is.EqualTo("\"12\"")),

			async ()=>Assert.That(await Request("/rpc/Rpc.Return([1])/file=test",null),Is.EqualTo("[1]"),"/file should work"),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return([\"/file=test\"])/pretty",null),Is.EqualTo("[\n\t\"/file=test\"\n]"),"/file within arguments should work"),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return([\"/file=test/pretty\"])",null),Is.EqualTo("[\"/file=test/pretty\"]"),"/pretty within arguments should not trip into pretty mode"),
			async ()=>Assert.That(await Request("/rpc/Rpc.Return(\"/file=test\")/download=test2",null),Is.EqualTo("/file=test"),"/download must work with /file as argument"),
			async ()=>StringAssert.DoesNotStartWith(nameof(RpcDataException),await Request("/rpc/Rpc./file=test",null),"/file should also work without explicit brackets for full function calls"),
		];

		foreach(var task in tests.Select(func=>func()).ToArray()) await task;
	});


	[OneTimeTearDown]
	public static void TearDown(){
		HttpClient.Dispose();
	}
}