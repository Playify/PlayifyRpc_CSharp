using PlayifyRpc.Types.Exceptions;

namespace PlayifyRpc.Types.Data.Objects;

public abstract class ObjectTemplateExcact:ObjectTemplateBase{
	public override bool TrySetProperty(string key,object? value,bool throwOnError)
		=>TrySetReflectionProperty(key,value,throwOnError)??
		  (throwOnError?throw new RpcDataException("Property \""+key+"\" does not exist on "+GetType().Name,null):false);

	public override bool TryGetProperty(string key,out object? value)=>TryGetReflectionProperty(key,out value);

	public override IEnumerable<(string key,object? value)> GetProperties()=>GetReflectionProperties();
}