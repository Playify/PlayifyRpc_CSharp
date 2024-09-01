using PlayifyRpc.Internal.Data;
using PlayifyUtility.Utils.Extensions;

namespace Tests;

public class MethodSignatures{
	public struct Struct<T>;

	public class Class<T>;


	public static void Test(){
		Console.WriteLine(DynamicTypeStringifier.FromType(typeof(bool)));
		Console.WriteLine(DynamicTypeStringifier.FromType(typeof((bool,bool?)?)));
		Console.WriteLine(DynamicTypeStringifier.FromType(typeof(bool?)));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(Power,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(Power2,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine();
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T1A,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T2A,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T3A,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T4A,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine();
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T1B,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T2B,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T3B,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T4B,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine();
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T1C,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T2C,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T3C,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T4C,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine();
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T1D,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T2D,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T3D,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));
		Console.WriteLine(DynamicTypeStringifier.MethodSignatures(T4D,false).Select(sig=>sig.parameters.Join(",")+"=>"+sig.returns).Join("\n"));


		static Task<bool> Power((bool,bool?)? b){
			return Task.FromResult(false);
		}

		static Task<bool> Power2(bool? b){
			return Task.FromResult(false);
		}

		static void T1A(Struct<Struct<bool>> b){}
		static void T2A(Struct<Class<bool>> b){}
		static void T3A(Class<Struct<bool>> b){}
		static void T4A(Class<Class<bool>> b){}
		static void T1B(Struct<Struct<bool>>? b){}
		static void T2B(Struct<Class<bool>>? b){}
		static void T3B(Class<Struct<bool>>? b){}
		static void T4B(Class<Class<bool>>? b){}
		static void T1C(Struct<Struct<bool>?> b){}
		static void T2C(Struct<Class<bool>?> b){}
		static void T3C(Class<Struct<bool>?> b){}
		static void T4C(Class<Class<bool>?> b){}
		static void T1D(Struct<Struct<bool>?>? b){}
		static void T2D(Struct<Class<bool>?>? b){}
		static void T3D(Class<Struct<bool>?>? b){}
		static void T4D(Class<Class<bool>?>? b){}
	}
}