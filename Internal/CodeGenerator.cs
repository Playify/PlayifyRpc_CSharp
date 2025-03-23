using System.Text;
using PlayifyRpc.Types;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

internal static class CodeGenerator{
	private static readonly string[] CSharpKeywords=[
		"abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
		"continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
		"false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
		"internal","is","lock","long","namespace","new","null","object","operator","out","override","params",
		"private","protected","public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc",
		"static","string","struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked",
		"unsafe","ushort","using","virtual","void","volatile","while",
	];

	private static string Quote(string s)=>JsonString.Escape(s);

	private static string Escape(string s){
		var escaped=new string(s.Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());
		if(escaped.Length==0||!char.IsLetter(escaped[0])&&escaped[0]!='_') escaped="_"+escaped;
		if(CSharpKeywords.Contains(escaped)) escaped="@"+escaped;

		return escaped;
	}

	public static Task<string> GenerateCSharp(string type)=>GenerateCSharp(new RpcObject(type));

	public static async Task<string> GenerateCSharp(RpcObject obj){
		var str=new StringBuilder();

		str.Append("[RpcConsumer(").Append(Quote(obj.Type)).Append(")]\n");
		str.Append("public static partial class ").Append(Escape(obj.Type)).Append("{\n");

		foreach(var method in (await Task.WhenAll((await obj.GetFunctions()).Select(GenerateCSharp))).SelectMany())
			str.Append('\t').Append(method).Append('\n');

		str.Append('}');
		return str.ToString();
	}

	private static async Task<IEnumerable<string>> GenerateCSharp(RpcFunction function)=>
		(await function.GetSignatures()).Select(tuple=>GenerateCSharp(function,tuple.returns,tuple.parameters));

	private static string GenerateCSharp(RpcFunction function,string returns,string[] parameters){
		var escape=Escape(function.Method);
		var str=new StringBuilder();
		if(escape!=function.Method) str.Append("[RpcNamed(").Append(Quote(function.Method)).Append(")]\n");
		str.Append("public static partial PendingCall");
		if(returns=="void") str.Append(' ');
		else str.Append('<').Append(returns).Append("> ");

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
}