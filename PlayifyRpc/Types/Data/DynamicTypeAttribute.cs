using JetBrains.Annotations;

namespace PlayifyRpc.Types.Data;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
//[BaseTypeRequired(typeof(IDynamicType))]
public class DynamicTypeAttribute:Attribute{
	internal readonly string Id;

	public DynamicTypeAttribute(string id)=>Id=id;
}

/*
public interface IDynamicType{
	//constructor(Incoming i);
	void Write(Outgoing o);
}*/