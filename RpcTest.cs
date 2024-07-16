using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;

namespace PlayifyRpc;

[RpcProvider]
public static class RpcTest{
	public class ClassA:ObjectTemplate{
		public string A;
	}

	public static string Test(ClassA a)=>"A";
	public static string Test(bool a)=>"B";
	public static string TestA(ClassA a)=>"A";
	public static string TestA2(ClassA a)=>"A";
	public static string TestA2(ClassA a,bool b)=>"A";

	public static string Def(bool b=false)=>"b";
	public static string Par(bool a,params bool[] b)=>"bb";
	public static string Par(bool a,ClassA b)=>"bA";
}