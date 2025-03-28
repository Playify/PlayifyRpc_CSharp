using PlayifyRpc.Types;

namespace PlayifyRpc.Internal.Data;

public enum ProgrammingLanguage{
	CSharp,
	TypeScript,
	JavaScript,
}

[RpcSetup]
public static class ProgrammingLanguageExtensions{
	static ProgrammingLanguageExtensions(){
		RpcData.Register<ProgrammingLanguage>(
			(pl,_)=>new RpcDataPrimitive((int)pl),
			(primitive,error)=>{
				if(primitive.IsBool(out var b)) return b?ProgrammingLanguage.TypeScript:ProgrammingLanguage.CSharp;
				if(primitive.IsString(out var s))
					return s.ToLowerInvariant() switch{
						"cs" or "csharp" or "c#"=>ProgrammingLanguage.CSharp,
						"ts" or "typescript"=>ProgrammingLanguage.TypeScript,
						"js" or "javascript" or "jsdoc"=>ProgrammingLanguage.JavaScript,
						_=>throw new ArgumentException(primitive+" is not a valid programming language."),
					};
				return primitive.TryTo(typeof(ProgrammingLanguage).GetEnumUnderlyingType(),out var result,error)?result:RpcData.ContinueWithNext;
			},(_,_)=>nameof(ProgrammingLanguage));
	}
}