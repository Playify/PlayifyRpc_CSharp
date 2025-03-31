using System.Text.RegularExpressions;
using JetBrains.Annotations;
using PlayifyRpc.Types.Data;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;
using static PlayifyRpc.Internal.Data.RpcTypeStringifier;

namespace Tests;

public static class MethodSignatures{
	[UsedImplicitly]
	private struct Struct<T>;

	[UsedImplicitly]
	private class Class<T>;

	[UsedImplicitly(ImplicitUseTargetFlags.Members)]
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
		Assert.That(FromType(typeof(ValueTuple<int>),true),Is.EqualTo("[number]"));
		Assert.That(FromType(typeof(ValueTuple),true),Is.EqualTo("[]"));
	});

	[Test]
	public static void Signatures(){
		Assert.Multiple(()=>{
			AssertSignature(MethodSignatures(Power),"((bool,bool?)? b)=>bool");
			AssertSignature(MethodSignatures(Power2),"(bool? b)=>bool");

			AssertSignature(MethodSignatures(T1A),"(Unknown<Struct<Unknown<Struct<bool>>>> b)=>void");
			AssertSignature(MethodSignatures(T2A),"(Unknown<Struct<Unknown<Class<bool>>>> b)=>void");
			AssertSignature(MethodSignatures(T3A),"(Unknown<Class<Unknown<Struct<bool>>>> b)=>void");
			AssertSignature(MethodSignatures(T4A),"(Unknown<Class<Unknown<Class<bool>>>> b)=>void");

			AssertSignature(MethodSignatures(T1B),"(Unknown<Struct<Unknown<Struct<bool>>>>? b)=>void");
			AssertSignature(MethodSignatures(T2B),"(Unknown<Struct<Unknown<Class<bool>>>>? b)=>void");
			AssertSignature(MethodSignatures(T3B),"(Unknown<Class<Unknown<Struct<bool>>>>? b)=>void");
			AssertSignature(MethodSignatures(T4B),"(Unknown<Class<Unknown<Class<bool>>>>? b)=>void");

			AssertSignature(MethodSignatures(T1C),"(Unknown<Struct<Unknown<Struct<bool>>?>> b)=>void");
			AssertSignature(MethodSignatures(T2C),"(Unknown<Struct<Unknown<Class<bool>>?>> b)=>void");
			AssertSignature(MethodSignatures(T3C),"(Unknown<Class<Unknown<Struct<bool>>?>> b)=>void");
			AssertSignature(MethodSignatures(T4C),"(Unknown<Class<Unknown<Class<bool>>?>> b)=>void");

			AssertSignature(MethodSignatures(T1D),"(Unknown<Struct<Unknown<Struct<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(T2D),"(Unknown<Struct<Unknown<Class<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(T3D),"(Unknown<Class<Unknown<Struct<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(T4D),"(Unknown<Class<Unknown<Class<bool>>?>>? b)=>void");
			AssertSignature(MethodSignatures(RealTypes),"(StringEnum<AnyEnum> se,bool b,dynamic any,Regex regex)=>int");
			AssertSignature(MethodSignatures(Params),"(int start,params int[] rest)=>void");
			AssertSignature(MethodSignatures(ParamsNull),"(params dynamic?[] rest)=>void");
			AssertSignature(MethodSignatures(Optional),"(int start)=>void\n(int start,int optional)=>void");
			AssertSignature(MethodSignatures(Fails),"(Unknown<Task> t)=>void");
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

	private static void Params(int start,params int[] rest){}//params inside anonymous function don't work somehow, but that's caused by c#, not by PlayifyRpc
	private static void ParamsNull(params object?[] rest){}
}