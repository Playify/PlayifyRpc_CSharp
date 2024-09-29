using JetBrains.Annotations;

namespace PlayifyRpc.Types.Data;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
//[BaseTypeRequired(typeof(IDynamicType))]
public class RpcDataTypeAttribute:Attribute{
	internal readonly string Id;

	public RpcDataTypeAttribute(string id){
		Id=id;
	}
}

/*
public interface IDynamicType{
	//constructor(DataInput i);
	void Write(DataOutput o);
}*/