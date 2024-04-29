using System.Dynamic;
using System.Reflection;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal;

public static partial class StaticallyTypedUtils{
	internal static IList<Type>? GetGenericTypeArguments(InvokeMemberBinder binder)
		=>Type.GetType("Mono.Runtime")!=null
			  ?binder
			   .GetType()
			   .GetField("typeArguments",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
			   ?.GetValue(binder) as IList<Type>
			  :binder
			   .GetType()
			   .GetInterface("Microsoft.CSharp.RuntimeBinder.ICSharpInvokeOrInvokeMemberBinder")
			   ?.GetProperty("TypeArguments")
			   ?.GetValue(binder,null) as IList<Type>;

	public static string Stringify(object? result,bool pretty){
		if(TryCast<Json>(result,out var json)) return json.ToString(pretty?"\t":null);
		if(TryCast<string>(result,out var s)) return s;

		return result switch{
			ExpandoObject expando when !expando.Any()=>"{}",
			ExpandoObject expando when pretty=>("{\n"+expando
			                                          .Select(pair=>JsonString.Escape(pair.Key)+":"+Stringify(pair.Value,true))
			                                          .Join(",\n")
			                                   ).Replace("\n","\n\t")+"\n}",
			ExpandoObject expando=>"{"+expando
			                           .Select(pair=>JsonString.Escape(pair.Key)+":"+Stringify(pair.Value,false))
			                           .Join(",")+"}",
			Array{Length: 0}=>"[]",
			Array array when pretty=>("[\n"+array.Cast<object?>().Select(o=>Stringify(o,true))
			                                     .Join(",\n")
			                         ).Replace("\n","\n\t")+"\n]",
			Array array=>"["+array.Cast<object?>().Select(o=>Stringify(o,false))
			                      .Join(",")+"]",
			null=>"null",
			float.NaN or double.NaN=>"NaN",
			// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
			_=>result.ToString()??"",
		};

	}
}