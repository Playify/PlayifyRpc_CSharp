using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PlayifyRpc.SourceGenerator;

[Generator]
public class RpcConsumerSourceGenerator:IIncrementalGenerator{//TODO check if working when using nuget
	private const string RpcNamedAttribute="PlayifyRpc.Types.RpcNamedAttribute";
	private const string RpcConsumerAttribute="PlayifyRpc.Types.RpcConsumerAttribute";
	// ReSharper disable once InconsistentNaming
	private const string IRpcConsumer=RpcConsumerAttribute+".IRpcConsumer";
	private const string RpcType="RpcType";

	public void Initialize(IncrementalGeneratorInitializationContext context){
		var classDeclarations=context.SyntaxProvider
		                             .CreateSyntaxProvider(
			                             static (node,_)=>node is ClassDeclarationSyntax{AttributeLists.Count: >0},
			                             static (ctx,_)=>(ClassDeclarationSyntax)ctx.Node
		                             )
		                             .Combine(context.CompilationProvider)
		                             .Select(Generate)
		                             .Where(static r=>r.HasValue)
		                             .Select(static (r,_)=>r!.Value);

		// Register source output
		context.RegisterSourceOutput(classDeclarations,
			static (ctx,generatedSource)=>
				ctx.AddSource($"{generatedSource.ClassName}.GeneratedRpc.cs",SourceText.From(generatedSource.SourceCode,Encoding.UTF8)));
	}

	private static (string ClassName,string SourceCode)? Generate((ClassDeclarationSyntax Left,Compilation Right) pair,CancellationToken cancel){
		var (classDecl,compilation)=pair;
		var semanticModel=compilation.GetSemanticModel(classDecl.SyntaxTree);

		if(compilation.GetTypeByMetadataName(RpcConsumerAttribute) is not{} rpcConsumerAttribute) return null;
		if(ModelExtensions.GetDeclaredSymbol(semanticModel,classDecl,cancel) is not INamedTypeSymbol classSymbol) return null;

		if(classSymbol.GetAttributes()
		              .FirstOrDefault(a=>SymbolEqualityComparer.Default.Equals(a.AttributeClass,rpcConsumerAttribute))
		   is not{} attrib) return null;

		var type=attrib.ConstructorArguments.IsEmpty||attrib.ConstructorArguments[0].IsNull
			         ?SymbolDisplay.FormatLiteral(classSymbol.Name,true)
			         :attrib.ConstructorArguments[0].ToCSharpString();

		return (classSymbol.ToDisplayString(),GenerateClass(classSymbol,type,compilation));
	}

	private static string GenerateClass(INamedTypeSymbol classSymbol,string type,Compilation compilation){
		var builder=new StringBuilder();

		//Namespace
		builder.Append("namespace ").Append(classSymbol.ContainingNamespace.ToDisplayString()).Append(";\r\n\r\n");

		//Class
		builder.Append(GenerateAccessibility(classSymbol.DeclaredAccessibility));
		if(classSymbol.IsStatic) builder.Append("static ");
		builder.Append("partial class ").Append(classSymbol.Name);
		if(!classSymbol.IsStatic) builder.Append(":").Append(IRpcConsumer);
		builder.Append("{\r\n");


		if(classSymbol.IsStatic){
			if(!classSymbol.GetMembers("RpcType").IsEmpty)
				builder.Append("\t//static ").Append(RpcType).Append(" is already defined\r\n");
			else
				builder.Append("\tstatic readonly string RpcType=").Append(type).Append(";\r\n");
		} else if(compilation.GetTypeByMetadataName(IRpcConsumer)?.GetMembers(RpcType).FirstOrDefault() is{} rpcTypeProperty
		          &&classSymbol.FindImplementationForInterfaceMember(rpcTypeProperty)!=null)
			builder.Append("\t//").Append(IRpcConsumer).Append(".").Append(RpcType).Append(" is already defined\r\n");
		else
			builder.Append("\tstring ").Append(IRpcConsumer).Append(".").Append(RpcType).Append("=>").Append(type).Append(";\r\n");


		if(classSymbol.IsStatic) type="RpcType";
		foreach(var member in classSymbol.GetMembers())
			GenerateMember(builder,member,type);

		builder.Append("\r\n\r\n}");
		return builder.ToString();
	}

	private static void GenerateMember(StringBuilder builder,ISymbol member,string staticType){
		const string instanceType="(("+IRpcConsumer+")this)."+RpcType;

		if(member is not IMethodSymbol method) return;
		if(!method.CanBeReferencedByName) return;
		if(!method.IsPartialDefinition) return;

		builder.Append("\r\n\r\n");

		var returnType=GenerateReturnType(method,out var unwrapped);


		//Warnings
		//if(!unwrapped) builder.Append("\t[System.Obsolete(\"This is synchronous. Use Task<?> or PendingCall<?> instead\")]\r\n");

		//Modifiers
		builder.Append("\t");
		builder.Append(GenerateAccessibility(method.DeclaredAccessibility));
		if(method.IsStatic) builder.Append("static ");
		builder.Append("partial ");

		//Method signature
		builder.Append(method.ReturnType);
		builder.Append(" ");
		builder.Append(method.Name);
		builder.Append("(");
		builder.Append(string.Join(",",method.Parameters.Select(p=>p.ToString())));
		builder.Append(")\r\n\t\t=>PlayifyRpc.Rpc.CallFunction");

		//Calling function
		if(returnType!=null) builder.Append("<").Append(returnType).Append(">");
		builder.Append("(");
		builder.Append(method.IsStatic?staticType:instanceType).Append(",");
		builder.Append(method.GetAttributes()
		                     .FirstOrDefault(a=>a.AttributeClass?.ToDisplayString()==RpcNamedAttribute)
			               is{} attrib
			               ?attrib.ConstructorArguments[0].ToCSharpString()
			               :SymbolDisplay.FormatLiteral(method.Name,true));


		//Parameter passing
		if(!method.Parameters.IsEmpty)
			// ReSharper disable once MergeIntoPattern
			if(method.Parameters.Length==1&&method.Parameters[0] is{Type: IArrayTypeSymbol{ElementType.SpecialType: SpecialType.System_Object}} objectArray)
				if(objectArray.IsParams) builder.Append(",").Append(method.Parameters[0].Name);
				else builder.Append(",new object?[]{").Append(method.Parameters[0].Name).Append("}");
			else if(method.Parameters.Last().IsParams)
				builder.Append(",new object?[]{")
				       .Append(string.Join(",",method.Parameters.Take(method.Parameters.Length-1).Select(p=>p.Name)))
				       .Append("}.Concat(").Append(method.Parameters.Last().Name).Append(".Cast<object?>()).ToArray()");
			else
				foreach(var parameter in method.Parameters)
					builder.Append(",").Append(parameter.Name);

		//Finishing
		builder.Append(unwrapped?");":").GetAwaiter().GetResult();");
	}

	private static string GenerateAccessibility(Accessibility accessibility)=>accessibility switch{
		Accessibility.Public=>"public ",
		Accessibility.Private=>"private ",
		Accessibility.Protected=>"protected ",
		Accessibility.Internal=>"internal ",
		Accessibility.ProtectedOrInternal=>"protected internal ",
		Accessibility.ProtectedAndInternal=>"private protected ",
		_=>"",
	};

	private static ITypeSymbol? GenerateReturnType(IMethodSymbol method,out bool unwrapped){
		unwrapped=false;
		if(method.ReturnsVoid) return null;
		if(method.ReturnType is not INamedTypeSymbol namedReturnType) return method.ReturnType;

		if(!(namedReturnType.ContainingNamespace.ToDisplayString() switch{
				    "System.Threading.Tasks"=>namedReturnType.Name is "Task" or "ValueTask",
				    "PlayifyRpc.Types.Functions"=>namedReturnType.Name is "PendingCall" or "PendingCallCasted",
				    _=>false,
			    })) return method.ReturnType;
		unwrapped=true;
		return namedReturnType.IsGenericType?namedReturnType.TypeArguments[0]:null;
	}
}