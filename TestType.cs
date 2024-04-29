using PlayifyRpc.Internal.Invokers;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Loggers;

namespace PlayifyRpc;

[RpcProvider]
public static class TestType{
	static TestType(){
		_=Rpc.RegisterType("TestDict",new DictionaryInvoker{
			{"test",()=>throw new Exception("TEST")},
			{"test2",Test0},
		});
		var x=new Logger().UseAsConsoleOut();
		Console.SetError(x);
	}

	public static void _Init(){
		Rpc.RegisterType("TestType",typeof(TestType));

		Rpc.Connect(new Uri("ws://192.168.0.12:4590/rpc"));

		new AutoResetEvent(false).WaitOne();
	}

	public static void Test0(){
		throw new Exception("T0");
	}

	public static void Test1(){
		throw new RpcException("T1");
	}

	public static void Test2(){
		var ex=new Exception("TEST");
		throw new RpcException("T2",ex);
	}

	public static void Test3(){
		var ex=new RpcException("TEST");
		throw new RpcException("T3",ex);
	}

	public static void Test4(){
		throw new RpcConnectionException("T4");
	}

	public static void Test5(){
		try{
			Test0();
		} catch(Exception e){
			throw new RpcException("T5",e);
		}
	}

	public static void Test6(){
		try{
			Test0();
		} catch(Exception e){
			throw new RpcException("T6",e);
		}
	}

	public static void Test8(){
		try{
			Test0();
		} catch(Exception e){
			throw new Exception("T8",e);
		}
	}

	public static void Test9(){
		try{
			Test0();
		} catch(Exception e){
			throw new RpcException("T9",e);
		}
	}

	public static async Task Test10(){
		try{
			await Rpc.CallLocal(Test0);
		} catch(Exception e){
			throw new RpcException("T10",e);
		}
	}

	public static async Task TestA0(){
		await Task.Yield();
		throw new Exception("T0");
	}

	public static async Task TestA1(){
		await Task.Yield();
		throw new RpcException("TA1");
	}

	public static async Task TestA2(){
		await Task.Yield();
		var ex=new Exception("TEST");
		throw new RpcException("TA2",ex);
	}

	public static async Task TestA3(){
		await Task.Yield();
		var ex=new RpcException("TEST");
		throw new RpcException("TA3",ex);
	}

	public static async Task TestA4(){
		await Task.Yield();
		throw new RpcConnectionException("TA4");
	}

	public static async Task TestA5(){
		try{
			await TestA0();
		} catch(Exception e){
			throw new RpcException("TA5",e);
		}
	}

	public static async Task TestA6(){
		try{
			await TestA0();
		} catch(Exception e){
			throw new RpcException("TA6",e);
		}
	}

	public static async Task TestA8(){
		try{
			await TestA0();
		} catch(Exception e){
			throw new Exception("TA8",e);
		}
	}

	public static async Task TestA9(){
		try{
			await TestA0();
		} catch(Exception e){
			throw new RpcException("TA9",e);
		}
	}

	public static async Task TestA10(){
		try{
			await Rpc.CallLocal(TestA0);
		} catch(Exception e){
			throw new RpcException("TA10",e);
		}
	}

	public static async Task TestA11(){
		try{
			await CallTest1();
		} catch(Exception e){
			throw new RpcException("TA11",e);
		}
	}

	public static async Task CallTest1(){
		await Rpc.CallFunction("TestType","TestA0");
	}

	public static async Task CallTest2(){
		await Rpc.CallLocal(TestA0);
	}

	public static async Task CallTest3(){
		await Rpc.CallLocal(CallTest2);
	}

	public static async Task CallTest4(){
		await Rpc.CallLocal(CallTest3);
	}

	public static async Task Clock(){
		var ctx=Rpc.GetContext();
		while(true){
			ctx.SendMessage(DateTime.Now);
			await Task.Delay(TimeSpan.FromSeconds(1),ctx.CancellationToken);
		}
	}
}