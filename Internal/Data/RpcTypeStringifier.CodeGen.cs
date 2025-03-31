using System.Reflection;
using System.Text;
using PlayifyRpc.Types;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcTypeStringifier{

	#region Method
	public static string GenerateMethod(Delegate method,ProgrammingLanguage lang)=>GenerateMethod(method.Method,lang);

	public static string GenerateMethod(MethodInfo method,ProgrammingLanguage lang)=>GenerateMethod(method.GetCustomAttribute<RpcNamedAttribute>()?.Name??method.Name,MethodSignatures(method,lang),lang);

	public static async Task<string> GenerateMethod(RpcFunction function,ProgrammingLanguage lang)=>GenerateMethod(function.Method,await function.GetSignatures(lang switch{
		ProgrammingLanguage.JavaScript=>ProgrammingLanguage.TypeScript,
		_=>lang,
	}),lang);


	private static string GenerateMethod(string name,IEnumerable<(string[] parameters,string returns)> signatures,ProgrammingLanguage lang){
		return lang switch{
			ProgrammingLanguage.CSharp=>signatures.Select(tuple=>GenerateCSharpMethod(name,tuple.returns,tuple.parameters)).Join('\n'),
			ProgrammingLanguage.TypeScript=>GenerateTypeScriptMethod(name,signatures.ToArray()),
			ProgrammingLanguage.JavaScript=>GenerateJavaScriptMethod(name,signatures.ToArray()),
			_=>throw new ArgumentOutOfRangeException(nameof(lang),lang,null),
		};
	}

	private static string GenerateCSharpMethod(string name,string returns,string[] parameters){
		var escape=EscapeCSharp(name);
		var str=new StringBuilder();
		if(escape!=name) str.Append("[RpcNamed(").Append(Quote(name)).Append(")]\n");
		str.Append("public static partial PendingCall");
		if(returns!="void") str.Append('<').Append(returns).Append('>');
		str.Append(' ');

		str.Append(escape).Append('(');
		var first=true;
		foreach(var parameter in parameters){
			if(first) first=false;
			else str.Append(',');
			str.Append(parameter);
		}
		str.Append(");");

		return str.ToString();
	}

	private static string GenerateJavaScriptMethod(string name,(string[] parameters,string returns)[] signatures){
		var str=new StringBuilder();
		str.Append("/** @type {").Append(GenerateTypeScriptType(signatures)).Append("}*/\n");
		str.Append(EscapeJavaScript(name)==name?name:Quote(name));
		if(signatures.Length!=1) str.Append("=(...params)=>{};");
		else
			str.Append("=(").Append(signatures[0].parameters.Select(p=>{
				var i=p.IndexOf(':');
				return i==-1?p:p.Substring(0,i);
			}).Join(',')).Append(")=>{};");

		return str.ToString();
	}

	private static string GenerateTypeScriptMethod(string name,(string[] parameters,string returns)[] signatures)
		=>$"{(EscapeJavaScript(name)==name?name:Quote(name))}:{GenerateTypeScriptType(signatures)}=null!;";

	private static string GenerateTypeScriptType((string[] parameters,string returns)[] signatures)
		=>signatures.Length switch{
			0=>"unknown",
			1=>$"({signatures[0].parameters.Join(',')})=>{signatures[0].returns}",
			_=>$"{{\n{signatures.Select(sig=>$"\t({sig.parameters.Join(',')}):{sig.returns},\n").ConcatString()}}}",
		};
	#endregion


	#region Class
	public static async Task<string> GenerateClass(RpcObject obj,ProgrammingLanguage lang)
		=>GenerateClass(obj.Type,await Task.WhenAll((await obj.GetFunctions()).Select(func=>GenerateMethod(func,lang))),lang);

	public static string GenerateClass(string name,string[] methods,ProgrammingLanguage lang){
		var methodsCombined=methods
		                    .Join("\n")
		                    .Replace("\r","")//Theoretically, no \r should occur
		                    .Replace("\n","\n\t");

		switch(lang){
			case ProgrammingLanguage.CSharp:{
				var str=new StringBuilder();
				str.Append("[RpcConsumer(").Append(Quote(name)).Append(")]\n");
				str.Append("public static partial class ").Append(EscapeCSharp(name)).Append("{\n");
				if(methodsCombined!="") str.Append('\t').Append(methodsCombined).Append('\n');

				str.Append('}');
				return str.ToString();
			}
			case ProgrammingLanguage.TypeScript:{
				var str=new StringBuilder();
				var escaped=EscapeJavaScript(name);
				str.Append("export const ").Append(escaped).Append("=Rpc.createObject(").Append(Quote(name)).Append(",new class ").Append(escaped).Append('{');
				if(methodsCombined!="") str.Append("\n\t").Append(methodsCombined).Append('\n');
				str.Append("});");
				return str.ToString();
			}
			case ProgrammingLanguage.JavaScript:{
				var str=new StringBuilder();
				var escaped=EscapeJavaScript(name);
				str.Append(escaped).Append("=Rpc.createObject(").Append(Quote(name)).Append(",new class ").Append(escaped).Append('{');
				if(methodsCombined!="") str.Append("\n\t").Append(methodsCombined).Append('\n');
				str.Append("});");
				return str.ToString();
			}
			default:throw new ArgumentOutOfRangeException(nameof(lang),lang,null);
		}
	}
	#endregion


	#region Helpers
	private static readonly HashSet<string> CSharpKeywords=[
		"abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
		"continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
		"false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
		"internal","is","lock","long","namespace","new","null","object","operator","out","override","params",
		"private","protected","public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc",
		"static","string","struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked",
		"unsafe","ushort","using","virtual","void","volatile","while",
	];
	private static readonly HashSet<string> JavaScriptKeywords=[
		"do","if","in","for","let","new","try","var","case","else","enum","eval","false","null","this","true",
		"void","with","break","catch","class","const","super","throw","while","yield","delete","export","import",
		"public","return","static","switch","typeof","default","extends","finally","package","private","continue",
		"debugger","function","arguments","interface","protected","implements","instanceof",
	];

	private static string Quote(string s)=>JsonString.Escape(s);

	private static string EscapeCSharp(string s){
		var escaped=new string(s.Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());
		if(escaped.Length==0||!char.IsLetter(escaped[0])&&escaped[0]!='_') escaped="_"+escaped;
		if(CSharpKeywords.Contains(escaped)) escaped="@"+escaped;

		return escaped;
	}

	private static string EscapeJavaScript(string s){
		var escaped=new string(s.Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());
		if(escaped.Length==0||!char.IsLetter(escaped[0])&&escaped[0]!='_') escaped="_"+escaped;
		if(JavaScriptKeywords.Contains(escaped)) escaped="@"+escaped;

		return escaped;
	}
	#endregion

}