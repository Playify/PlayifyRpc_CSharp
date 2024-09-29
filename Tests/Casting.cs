using PlayifyRpc.Internal.Data;
using PlayifyUtility.Jsons;

namespace Tests;

public class Casting{
	[SetUp]
	public void Setup(){
	}

	[Test]
	public void Nulls(){
		Assert.That(DynamicCaster.Cast<object>(JsonNull.Null),Is.EqualTo(null));
		Assert.That(DynamicCaster.Cast<object>(null),Is.EqualTo(null));
		Assert.That(DynamicCaster.Cast<Json>(null),Is.EqualTo(JsonNull.Null));
		Assert.That(DynamicCaster.Cast<Json>(DBNull.Value),Is.EqualTo(JsonNull.Null));
	}

	[Test]
	public void Nullables(){
		Assert.That(DynamicCaster.Cast<bool>(JsonBool.True),Is.EqualTo(true));
		Assert.That(DynamicCaster.Cast<bool?>(DBNull.Value),Is.EqualTo(null));
		Assert.That(DynamicCaster.Cast<bool?>(JsonBool.False),Is.EqualTo(false));

		Assert.That(()=>DynamicCaster.Cast<JsonNull>(false),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>DynamicCaster.Cast<bool>(null),Throws.TypeOf<InvalidCastException>());
	}

	[Test]
	public void Primitives(){
		Assert.That(DynamicCaster.Cast<double>((long)4),Is.EqualTo((double)4));
		Assert.That(DynamicCaster.Cast<int>(10e1),Is.EqualTo(100));

		Assert.That(()=>DynamicCaster.Cast<bool>(1),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>DynamicCaster.Cast<int>(true),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>DynamicCaster.Cast<int>(1.5f),Throws.TypeOf<InvalidCastException>());
		Assert.That(()=>DynamicCaster.Cast<int>(uint.MaxValue),Throws.TypeOf<InvalidCastException>());
	}
}