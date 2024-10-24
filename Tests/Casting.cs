using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using PlayifyRpc;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace Tests;

public class Casting{

	[SetUp]
	public void Setup(){
		typeof(Rpc).RunClassConstructor();
	}

	[Test]
	public void General()=>Assert.Multiple(()=>{
		Assert.That(new RpcDataPrimitive(1ul),Is.EqualTo(new RpcDataPrimitive(1)));
		Assert.That(new RpcDataPrimitive("Q"),Is.EqualTo(new RpcDataPrimitive('Q')));
		//TODO Assert.That(RpcDataPrimitive.Number(1d),Is.EqualTo(RpcDataPrimitive.Number(1ul)));
	});

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
		var customType=RpcDataPrimitive.Cast<ExampleObjectType>(json);
		Assert.That(customType.a,Is.EqualTo(stringMap["a"]));
		Assert.That(customType.A,Is.EqualTo(stringMap["A"]));
		Assert.That(RpcDataPrimitive.Cast<Json>(customType).ToString(),Is.EqualTo(json.ToString()));
	});


	[UsedImplicitly]
	[SuppressMessage("ReSharper","InconsistentNaming")]
	private class ExampleObjectType:RpcDataObject{
#pragma warning disable CS0649// Field is never assigned to, and will always have its default value
		public string? a;
		public string? A;
#pragma warning restore CS0649// Field is never assigned to, and will always have its default value
	}
}