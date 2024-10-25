using PlayifyRpc;
using PlayifyRpc.Types;

namespace Tests;

public static class DynamicInvoking{
	private static class TestClass{
		public static int Func(int i)=>i;
		public static bool ObjectAutoCast(object i)=>i is int;
	}

	[SetUp]
	public static void Setup(){
		Rpc.RegisterType("TestClass",typeof(TestClass));
	}

	[Test]
	public static void Dynamic()=>Assert.Multiple(async ()=>{
		Assert.That(await Rpc.CallFunction<int>("TestClass",nameof(TestClass.Func),1),Is.EqualTo(1));
		Assert.That(await Rpc.CallFunction<bool>("TestClass",nameof(TestClass.ObjectAutoCast),1),
			Is.True,"object parameters should be automatically casted to some valid primitive type instead of RpcDataPrimitive");

		dynamic obj=Rpc.CreateObject("TestClass");
		var func=obj.Func;
		RpcFunction _=obj.Func;
		Assert.That(obj is RpcObject,Is.True);
		Assert.That(func is RpcFunction,Is.True);
		Assert.AreEqual(1,await obj.Func<int>(1));
	});
}