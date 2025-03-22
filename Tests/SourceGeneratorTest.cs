using PlayifyRpc;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Functions;

namespace Tests;

[RpcConsumer]
[RpcSetup]
public partial class SourceGeneratorTest{
	public partial int Add(int a,int b);
	public partial Task<int> Max(int pre,params int[] a);
	public partial PendingCall<StringMap> Array(object[] arr);
	public partial PendingCall Params(params object[] arr);
	public partial PendingCall Def(int a=0);
	public static partial PendingCall<StringMap> ArrayNull(object?[] arr);
	public static partial PendingCall ParamsNull(params object?[] arr);

	[RpcNamed("Test")]
	public partial ValueTask Named();

	public Task<int> Max2(int pre,params int[] a)=>Rpc.CallFunction<int>(((RpcConsumerAttribute.IRpcConsumer)this).RpcType,"",new object[]{pre}.Concat(a.Cast<object>()).ToArray());

	//static string RpcType=>"";
}

[RpcConsumer]
[RpcSetup]
public partial class SourceGeneratorTest2:SourceGeneratorTest{

	public partial int Add2(int a,int b);

	public SourceGeneratorTest2(){
		Add2(1,1);
	}

	public static void Test(){
		new SourceGeneratorTest2();
	}

	private string RpcType=>"TEST2";
}