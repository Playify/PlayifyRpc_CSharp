using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using PlayifyRpc;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Utils;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace Tests;

public class Casting{
	[SuppressMessage("ReSharper","InconsistentNaming")]
	private class ExampleObjectType:RpcDataObject{
		public string? a;
		public string? A;
	}

	[SuppressMessage("ReSharper","InconsistentNaming")]
	private class ReducedObjectType:RpcDataObject{
		public string? a;
	}

	[SuppressMessage("ReSharper","InconsistentNaming")]
	[UsedImplicitly(ImplicitUseTargetFlags.Members)]
	private struct ExampleStructType:IRpcDataObject{
		public readonly string? a;
		public readonly string? A;

		public bool TrySetProps(IEnumerable<(string key,RpcDataPrimitive value)> props,bool throwOnError,RpcDataPrimitive original)
			=>RpcDataObject.Reflection.SetProps(ref this,props,throwOnError,original);

		public IEnumerable<(string key,RpcDataPrimitive value)> GetProps(Dictionary<object,RpcDataPrimitive> already)
			=>RpcDataObject.Reflection.GetProps(this,already);
	}

	[SetUp]
	public void Setup(){
		typeof(Rpc).RunClassConstructor();
	}

	[Test]
	public void Nulls()=>Assert.Multiple(()=>{
		Assert.That(RpcDataPrimitive.Cast<object>(JsonNull.Null),Is.EqualTo(null));
		Assert.That(RpcDataPrimitive.Cast<object>(null),Is.EqualTo(null));
		Assert.That(RpcDataPrimitive.Cast<Json>(null),Is.EqualTo(JsonNull.Null));
		Assert.That(RpcDataPrimitive.Cast<Json>(DBNull.Value),Is.EqualTo(JsonNull.Null));
	});

	[Test]
	public void Nullables()=>Assert.Multiple(()=>{
		Assert.That(RpcDataPrimitive.Cast<bool>(JsonBool.True),Is.EqualTo(true));
		Assert.That(RpcDataPrimitive.Cast<bool?>(DBNull.Value),Is.EqualTo(null));
		Assert.That(RpcDataPrimitive.Cast<bool?>(JsonBool.False),Is.EqualTo(false));

		Assert.That(()=>RpcDataPrimitive.Cast<JsonNull>(false),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>RpcDataPrimitive.Cast<bool>(null),Throws.TypeOf<InvalidCastException>());

		Assert.That(RpcDataPrimitive.Cast<string>(null),Is.EqualTo(null));
	});

	[Test]
	public void Primitives()=>Assert.Multiple(()=>{
		Assert.That(RpcDataPrimitive.Cast<double>((long)4),Is.EqualTo((double)4));
		Assert.That(RpcDataPrimitive.Cast<int>(10e1),Is.EqualTo(100));

		Assert.That(()=>RpcDataPrimitive.Cast<bool>(1),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>RpcDataPrimitive.Cast<int>(true),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>RpcDataPrimitive.Cast<int>(1.5f),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>RpcDataPrimitive.Cast<int>(uint.MaxValue),Throws.TypeOf<InvalidCastException>());
	});

	[Test]
	public void Cyclic()=>Assert.Multiple(()=>{
		var list=new object[1];
		list[0]=list;

		Assert.That(RpcDataPrimitive.From(list).ToString(false),Is.EqualTo("[<<Cyclic Reference>>]"));
	});

	[Test]
	public void ToStringOfPrimitives()=>Assert.Multiple(()=>{Assert.That(RpcDataPrimitive.From(new byte[]{0,1,2}).ToString(false),Is.EqualTo("[0,1,2]"));});


	private enum IntEnum{
		Small=12,
		Big=500,
	}

	private enum ByteEnum:byte{
		Small=12,
	}

	[Test]
	public void Enums()=>Assert.Multiple(()=>{
		Assert.That(RpcDataPrimitive.Cast<ByteEnum>(IntEnum.Small),Is.EqualTo(ByteEnum.Small));
		Assert.That(RpcDataPrimitive.Cast<IntEnum>(ByteEnum.Small),Is.EqualTo(IntEnum.Small));
		Assert.That(RpcDataPrimitive.From(new StringEnum<ByteEnum>(ByteEnum.Small)),Is.EqualTo(new RpcDataPrimitive(nameof(ByteEnum.Small))));
		Assert.That(RpcDataPrimitive.Cast<int>(IntEnum.Big),Is.EqualTo(500));

		Assert.That(()=>RpcDataPrimitive.Cast<ByteEnum>(IntEnum.Big),Throws.TypeOf<InvalidCastException>());
	});

	[Test]
	public void Objects()=>Assert.Multiple(()=>{
		var json=new JsonObject{
			{"a","small"},
			{"A","big"},
		};
		var stringMap=new StringMap<string>{
			{"a","small"},
			{"A","big"},
		};

		Assert.That(RpcDataPrimitive.Cast<Json>(stringMap).ToString(),Is.EqualTo(json.ToString()));
		var customObject=RpcDataPrimitive.Cast<ExampleObjectType>(json);
		Assert.That(customObject.a,Is.EqualTo(stringMap["a"]));
		Assert.That(customObject.A,Is.EqualTo(stringMap["A"]));
		Assert.That(RpcDataPrimitive.Cast<Json>(customObject).ToString(),Is.EqualTo(json.ToString()));
		var customStruct=RpcDataPrimitive.Cast<ExampleStructType>(json);
		Assert.That(customStruct.a,Is.EqualTo(stringMap["a"]));
		Assert.That(customStruct.A,Is.EqualTo(stringMap["A"]));
		Assert.That(RpcDataPrimitive.Cast<Json>(customStruct).ToString(),Is.EqualTo(json.ToString()));
	});

	[Test]
	public void Errors()=>Assert.Multiple(()=>{
		var obj=new StringMap<object>{
			{
				"a",new object[]{
					new Regex("TEST"),//Not supported by json
				}
			},
		};
		var exception=Assert.Throws<InvalidCastException>(()=>RpcDataPrimitive.Cast<Json>(obj))!;
		StringAssert.Contains("index 0",exception.ToString());
		StringAssert.Contains("property \"a\"",exception.ToString());

		//If property is not found, it should appear in the stack trace
		StringAssert.Contains("property \"A\"",Assert.Throws<InvalidCastException>(()=>RpcDataPrimitive.Cast<ReducedObjectType>(new ExampleObjectType()))!.ToString());
	});


	[Test]
	public void Cloning()=>Assert.Multiple(()=>{
		var @base=new StringMap<Json[]>{
			{
				"a",[
					new JsonObject().Push(out var inner),
				]
			},
		};
		var clone=@base.Clone();
		Assert.That(clone,Is.EqualTo(@base));//Structure should stay the same
		Assert.That(clone,Is.Not.SameAs(@base));//direct childs should change
		Assert.That(inner,Is.Not.SameAs(clone["a"][0]));//deep childs should also change
	});
}