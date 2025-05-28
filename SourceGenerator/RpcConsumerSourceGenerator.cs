using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PlayifyRpc.SourceGenerator;

[Generator]
public class RpcConsumerSourceGenerator:IIncrementalGenerator{
	private const string RpcNamedAttribute="PlayifyRpc.Types.RpcNamedAttribute";
	private const string RpcConsumerAttribute="PlayifyRpc.Types.RpcConsumerAttribute";
	// ReSharper disable once InconsistentNaming
	private const string IRpcConsumer=RpcConsumerAttribute+".IRpcConsumer";
	private const string RpcDataTransformerAttribute="PlayifyRpc.Types.Data.RpcDataTransformerAttribute";
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
		builder.Append(' ');
		builder.Append(method.Name);
		if(method.IsGenericMethod){
			builder.Append('<');
			builder.Append(string.Join(",",method.TypeParameters.Select(t=>t.Name)));
			builder.Append('>');
		}

		builder.Append('(');
		builder.Append(string.Join(",",method.Parameters.Select(p=>p.ToString())));
		builder.Append(')');
		builder.Append(GenerateTypeConstraints(method));

		var returnAttributes=method.GetReturnTypeAttributes().Any(FilterTransformerAttribute);
		var parameterAttributes=method.Parameters.Select(p=>p.GetAttributes().Any(FilterTransformerAttribute)).ToArray();
		var anyParameterAttributes=parameterAttributes.Any(b=>b);

		builder.Append("{\r\n\t\t");
		var methodVar="__"+method.Name+"_method";
		var parametersVar="__"+method.Name+"_parameters";
		if(returnAttributes||anyParameterAttributes)
			builder.Append("var ").Append(methodVar).Append("=(System.Reflection.MethodInfo)System.Reflection.MethodBase.GetCurrentMethod()!;\r\n\t\t");
		if(anyParameterAttributes)
			builder.Append("var ").Append(parametersVar).Append('=').Append(methodVar).Append(".GetParameters();\r\n\t\t");

		if(!method.ReturnsVoid) builder.Append("return ");
		builder.Append("PlayifyRpc.Rpc.CallFunction");


		//Calling function
		if(returnType!=null&&!returnAttributes) builder.Append("<").Append(returnType).Append(">");
		builder.Append("(\r\n\t\t\t");
		builder.Append(method.IsStatic?staticType:instanceType).Append(",");
		builder.Append(method.GetAttributes()
		                     .FirstOrDefault(a=>a.AttributeClass?.ToDisplayString()==RpcNamedAttribute)
			               is{} attrib
			               ?attrib.ConstructorArguments[0].ToCSharpString()
			               :SymbolDisplay.FormatLiteral(method.Name,true));

		//Parameter passing
		if(!method.Parameters.IsEmpty){
			var useArray=!method.Parameters[0].IsParams;
			builder.Append(useArray?",\r\n\t\t\tnew object?[]{":",\r\n\t\t\t");

			var closed=false;
			for(var i=0;i<method.Parameters.Length;i++){
				if(!method.Parameters[i].IsParams){
					if(i!=0) builder.Append(",");
					builder.Append("\r\n\t\t\t");
					if(useArray) builder.Append("\t");
					builder.Append(Parameter(i));
					continue;
				}
				closed=true;
				if(i!=0) builder.Append("\r\n\t\t\t}.Concat(");
				if(parameterAttributes[i])
					builder.Append($"{RpcConsumerAttribute}.TransformArray({method.Parameters[i].Name},{parametersVar}[{i}])");
				else{
					builder.Append(method.Parameters[i].Name);
					if(method.Parameters[i].Type is not IArrayTypeSymbol{ElementType.SpecialType: SpecialType.System_Object})
						builder.Append(".Cast<object?>()");
				}
				if(i!=0) builder.Append(").ToArray()");
			}
			if(!closed) builder.Append("\r\n\t\t\t}");

			string Parameter(int i,string? name=null){
				return parameterAttributes[i]
					       ?$"{RpcConsumerAttribute}.Transform({name??method.Parameters[i].Name},{parametersVar}[{i}])"
					       :name??method.Parameters[i].Name;
			}
		}
		builder.Append("\r\n\t\t");

		//Finishing
		builder.Append(')');
		if(returnAttributes){
			if(returnType!=null) builder.Append("\r\n\t\t\t.Cast<").Append(returnType).Append(">");
			else builder.Append("\r\n\t\t\t.Void");
			builder.Append("(System.Reflection.CustomAttributeExtensions.GetCustomAttribute<")
			       .Append(RpcDataTransformerAttribute).Append(">(").Append(methodVar).Append(".ReturnParameter))");
		}
		if(!unwrapped) builder.Append(".GetAwaiter().GetResult()");
		builder.Append(";\r\n\t}");
	}

	private static bool FilterTransformerAttribute(AttributeData attribute){
		for(var cls=attribute.AttributeClass;cls!=null;cls=cls.BaseType)
			if(cls.ToDisplayString()==RpcDataTransformerAttribute)
				return true;
		return false;
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

	private static string GenerateTypeConstraints(IMethodSymbol method){
		if(!method.IsGenericMethod)
			return string.Empty;

		var constraints=new List<string>();

		foreach(var tp in method.TypeParameters){
			var parts=new List<string>();

			// Value type / Reference type / Unmanaged
			if(tp.HasValueTypeConstraint)
				parts.Add("struct");
			else if(tp.HasReferenceTypeConstraint)
				parts.Add("class");

			// Specific base types or interfaces
			foreach(var constraintType in tp.ConstraintTypes){
				parts.Add(constraintType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
			}

			// Constructor constraint
			if(tp.HasConstructorConstraint)
				parts.Add("new()");

			if(parts.Count>0)
				constraints.Add($"where {tp.Name} : {string.Join(", ",parts)}");
		}

		return constraints.Count>0
			       ?"\r\n\t"+string.Join("\r\n\t",constraints)
			       :string.Empty;
	}

}