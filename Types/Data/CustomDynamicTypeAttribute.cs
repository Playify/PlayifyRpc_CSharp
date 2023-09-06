using JetBrains.Annotations;

namespace PlayifyRpc.Types.Data;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
//[BaseTypeRequired(typeof(IDynamicType))]
public class CustomDynamicTypeAttribute:Attribute{
	internal readonly string Id;

	public CustomDynamicTypeAttribute(string id)=>Id=id;
}

/*
public interface IDynamicType{
	//constructor(Incoming i);
	void Write(Outgoing o);
}*/