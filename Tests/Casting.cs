using PlayifyRpc;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data;
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


	public enum IntEnum{
		Small=12,
		Big=500,
	}

	public enum ByteEnum:byte{
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

	//TODO custom objects
}