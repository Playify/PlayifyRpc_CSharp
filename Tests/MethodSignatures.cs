using System.Text.RegularExpressions;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;
using static PlayifyRpc.Internal.Data.RpcDataTypeStringifier;

namespace Tests;

public static class MethodSignatures{
	private struct Struct<T>;

	private class Class<T>;

	private enum AnyEnum{
		A=1,
	}

	private static void AssertSignature(IEnumerable<(string[] parameters,string returns)> signature,string expected){
		var s=signature.Select(sig=>$"({sig.parameters.Join(",")})=>{sig.returns}").Join("\n");
		Assert.That(s,Is.EqualTo(expected));
	}


	[Test]
	public static void Types()=>Assert.Multiple(()=>{
		Assert.That(FromType(typeof(bool)),Is.EqualTo("bool"));
		Assert.That(FromType(typeof((bool,bool?)?)),Is.EqualTo("(bool,bool?)?"));
		Assert.That(FromType(typeof(bool?)),Is.EqualTo("bool?"));
		Assert.That(FromType(typeof(byte[]),true),Is.EqualTo("Uint8Array"));
	});

	[Test]
	public static void Signatures(){
		Assert.Multiple(()=>{
			AssertSignature(MethodSignatures(Power,false),"((bool,bool?)? b)=>bool");
			AssertSignature(MethodSignatures(Power2,false),"(bool? b)=>bool");

			AssertSignature(MethodSignatures(T1A,false),"(Unknown<Struct<Unknown<Struct<bool>>>> b)=>void");
			AssertSignature(MethodSignatures(T2A,false),"(Unknown<Struct<Unknown<Class<bool>>>> b)=>void");
			AssertSignature(MethodSignatures(T3A,false),"(Unknown<Class<Unknown<Struct<bool>>>> b)=>void");
			AssertSignature(MethodSignatures(T4A,false),"(Unknown<Class<Unknown<Class<bool>>>> b)=>void");

			AssertSignature(MethodSignatures(T1B,false),"(Unknown<Struct<Unknown<Struct<bool>>>>? b)=>void");
			AssertSignature(MethodSignatures(T2B,false),"(Unknown<Struct<Unknown<Class<bool>>>>? b)=>void");
			AssertSignature(MethodSignatures(T3B,false),"(Unknown<Class<Unknown<Struct<bool>>>>? b)=>void");
			AssertSignature(MethodSignatures(T4B,false),"(Unknown<Class<Unknown<Class<bool>>>>? b)=>void");

			AssertSignature(MethodSignatures(T1C,false),"(Unknown<Struct<Unknown<Struct<bool>>?>> b)=>void");
			AssertSignature(MethodSignatures(T2C,false),"(Unknown<Struct<Unknown<Class<bool>>?>> b)=>void");
			AssertSignature(MethodSignatures(T3C,false),"(Unknown<Class<Unknown<Struct<bool>>?>> b)=>void");
			AssertSignature(MethodSignatures(T4C,false),"(Unknown<Class<Unknown<Class<bool>>?>> b)=>void");

			AssertSignature(MethodSignatures(T1D,false),"(Unknown<Struct<Unknown<Struct<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(T2D,false),"(Unknown<Struct<Unknown<Class<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(T3D,false),"(Unknown<Class<Unknown<Struct<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(T4D,false),"(Unknown<Class<Unknown<Class<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(RealTypes,false),"(StringEnum<AnyEnum> se,bool b,dynamic any,Regex regex)=>int");
			AssertSignature(MethodSignatures(typeof(MethodSignatures).GetMethod(nameof(Params))!,false),"(int start,params int[] rest)=>void");
			AssertSignature(MethodSignatures(Optional,false),"(int start)=>void\n(int start,int optional)=>void");
			AssertSignature(MethodSignatures(Fails,false),"(Unknown<Task> t)=>void");
		});
		return;

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

		static int RealTypes(StringEnum<AnyEnum> se,JsonBool b,Json any,Regex regex){
			return 0;
		}

		static void Optional(int start,int optional=0){}
		static void Fails(Task t){}
	}

	public static void Params(int start,params int[] rest){}
}