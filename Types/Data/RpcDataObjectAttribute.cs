using JetBrains.Annotations;
using PlayifyRpc.Internal.Data;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Types.Data;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class RpcDataObjectAttribute:Attribute{
	private readonly bool _requireAllProps;

	public RpcDataObjectAttribute(bool requireAllProps){
		_requireAllProps=requireAllProps;
	}
}

public class RpcDataObject:IRpcDataObject{
	bool IRpcDataObject.TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)=>false;
	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetExtraProps(Dictionary<object,RpcDataPrimitive> already)=>[];
}

public class RpcDataObjectExtraProps:IRpcDataObject{
	public readonly InsertionOrderDictionary<string,RpcDataPrimitive> ExtraProps=new();
	bool IRpcDataObject.TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)=>ExtraProps.TryAdd(s,primitive);
	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetExtraProps(Dictionary<object,RpcDataPrimitive> already)=>ExtraProps.ToTuples();
}

public interface IRpcDataObject{
	bool TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError);
	IEnumerable<(string key,RpcDataPrimitive value)> GetExtraProps(Dictionary<object,RpcDataPrimitive> already);
}

public interface IRpcDataObjectRequireAllProps:IRpcDataObject;

[RpcDataObject(false)]
public class StringMap<T>:InsertionOrderDictionary<string,T>,IRpcDataObject{

	bool IRpcDataObject.TrySetExtraProp(string s,RpcDataPrimitive primitive,bool throwOnError)
		=>primitive.TryTo(out T child,throwOnError)&&this.TryAdd(s,child);

	IEnumerable<(string key,RpcDataPrimitive value)> IRpcDataObject.GetExtraProps(Dictionary<object,RpcDataPrimitive> already)
		=>this.Select(kv=>(kv.Key,RpcDataPrimitive.From(kv.Value,already)));
}