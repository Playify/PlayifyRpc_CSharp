using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
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
	public static partial Task VoidTask();
	public static partial void Void();

	[RpcNamed("Test")]
	public partial ValueTask Named();

	public partial Task<int> Max2([RpcDataTransformerAttribute.RpcDataNullTransformer]params int[] a);
	public partial Task<int> Max2([RpcDataTransformerAttribute.RpcDataNullTransformer]params object[] args);
	public partial Task<int> Max2(int prev,[RpcDataTransformerAttribute.RpcDataNullTransformer]params int[] a);
	public partial Task<int> Max2(int prev,[RpcDataTransformerAttribute.RpcDataNullTransformer]params object[] args);
	public partial Task<int> Max3([RpcDataTransformerAttribute.RpcDataNullTransformer]int a);
	public partial Task<int> Max3([RpcDataTransformerAttribute.RpcDataNullTransformer]object args);
	public partial Task<int> Max3(int prev,[RpcDataTransformerAttribute.RpcDataNullTransformer]int a);
	public partial Task<int> Max3(int prev,[RpcDataTransformerAttribute.RpcDataNullTransformer]object args);

	//static string RpcType=>"";
}

[RpcConsumer]
[RpcSetup]
public partial class SourceGeneratorTest2:SourceGeneratorTest{

	public partial int Add2(int a,[RpcDataTransformerAttribute.RpcDataNullTransformer]int b);


	[return: RpcDataTransformerAttribute.RpcDataNullTransformer]
	public partial int Add3<T>(T a,int b);

	public partial T Add4<T>(int a,int b) where T : class;
	public partial void Add5<T>(T a,int b);

	[return: RpcDataTransformerAttribute.RpcDataNullTransformer]
	public partial void Add6<T>(T a,int b);


	public SourceGeneratorTest2(){
		Add2(1,1);
	}

	public static void Test(){
		new SourceGeneratorTest2();
	}

	private string RpcType=>"TEST2";
}