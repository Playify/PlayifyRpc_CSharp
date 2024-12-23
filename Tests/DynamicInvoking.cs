using PlayifyRpc;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;

namespace Tests;

public static class DynamicInvoking{
	private static class TestClass{
		public static int Func(int i)=>i;
		public static bool ObjectAutoCast(object i)=>i is int;
		public static int[] Defaults(int a,int b=1,params int[] c)=>[a,b,..c];
		public static bool Fcc(FunctionCallContext? ctx)=>ctx!=null;

		public static bool Specific(string s)=>true;

		public static bool Specific(RpcDataPrimitive s)=>false;
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
		CollectionAssert.AreEquivalent(await Rpc.CallFunction<int[]>("TestClass",nameof(TestClass.Defaults),4),(int[])[4,1]);
		CollectionAssert.AreEquivalent(await Rpc.CallFunction<int[]>("TestClass",nameof(TestClass.Defaults),4,8),(int[])[4,8]);
		CollectionAssert.AreEquivalent(await Rpc.CallFunction<int[]>("TestClass",nameof(TestClass.Defaults),4,2,9),(int[])[4,2,9]);
		CollectionAssert.AreEquivalent(await Rpc.CallFunction<int[]>("TestClass",nameof(TestClass.Defaults),4,2,4,5),(int[])[4,2,4,5]);
		Assert.That(await Rpc.CallFunction<bool>("TestClass",nameof(TestClass.Fcc)),Is.True,nameof(FunctionCallContext)+" arguments should be auto filled in");
		Assert.That(await Rpc.CallFunction<bool>("TestClass",nameof(TestClass.Specific),""),Is.True,"specific types should be prefered over RpcDataPrimitive arguments");

		dynamic obj=Rpc.CreateObject("TestClass");
		var func=obj.Func;
		RpcFunction _=obj.Func;
		Assert.That(obj is RpcObject,Is.True);
		Assert.That(func is RpcFunction,Is.True);
		Assert.AreEqual(1,await obj.Func<int>(1));
	});

	[Test]
	public static void Exceptions()=>Assert.Multiple(()=>{
		StringAssert.DoesNotContain("Invoker.",Assert.ThrowsAsync<RpcException>(()=>Rpc.CallLocal(()=>throw new Exception()).ToTask())!.StackTrace);
	});
}