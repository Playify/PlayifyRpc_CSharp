using System.Reflection;
using System.Runtime.CompilerServices;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public static class RpcDataTypeStringifier{
	public static readonly Dictionary<Type,KnownFunc> ToStringDictionary=new();
	public static readonly List<UnknownFunc> ToStringList=[];

	public delegate string KnownFunc(bool typescript,string[] generics);

	public delegate string? UnknownFunc(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics);

	static RpcDataTypeStringifier()=>RpcSetupAttribute.LoadAll();


	public static string FromType(Type type,bool typescript=false)=>Stringify(type,typescript,true,()=>null,null);

	public static IEnumerable<(string[] parameters,string returns)> MethodSignatures(Delegate method,bool typescript,params string[] prevParameters)
		=>MethodSignatures(method.Method,typescript,prevParameters);

	public static IEnumerable<(string[] parameters,string returns)> MethodSignatures(MethodInfo method,bool typescript,params string[] prevParameters){
		// ReSharper disable once RedundantSuppressNullableWarningExpression
		var returns=ParameterType(method.ReturnParameter!,false,typescript);
		var list=new List<string>(prevParameters);

		var filteredFcc=false;

		foreach(var parameter in method.GetParameters()){
			if(parameter.IsOptional) yield return (list.ToArray(),returns);
			if(parameter.IsOut||parameter.ParameterType.IsByRef) yield break;//'ref' and 'out' is not supported

			if(!filteredFcc&&parameter.ParameterType==typeof(FunctionCallContext)){
				filteredFcc=true;
				continue;//Skip this argument (once)
			}

			var @params=parameter.ParameterType.IsArray&&parameter.IsDefined(typeof(ParamArrayAttribute),true)?typescript?"...":"params ":"";
			var parameterType=ParameterType(parameter,true,typescript);
			list.Add(@params+Parameter(typescript,parameterType,parameter.Name));
		}
		yield return (list.ToArray(),returns);
	}


	private static string ParameterType(ParameterInfo parameter,bool input,bool typescript){
		var tupleNames=parameter.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames;
		var tupleIndex=0;

		var type=parameter.ParameterType;
		var nullability=new NullabilityInfoContext().Create(parameter);

		if(!input)
			while(true)
				if(type==typeof(void)||type==typeof(VoidType)||type==typeof(Task)||type==typeof(ValueTask)) return "void";
				else if(type.IsGenericType&&(type.GetGenericTypeDefinition()==typeof(Task<>)||type.GetGenericTypeDefinition()==typeof(ValueTask<>))){
					type=type.GetGenericArguments()[0];
					nullability=nullability?.GenericTypeArguments[0];
				} else break;

		return Stringify(type,typescript,input,()=>tupleNames?[tupleIndex++],nullability);
	}

	public static string Stringify(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability){
		var isNullable=(input?nullability?.ReadState:nullability?.WriteState)==NullabilityState.Nullable;
		if(Nullable.GetUnderlyingType(type) is{} underlying){
			isNullable=true;
			type=underlying;
		}
		var suffix=isNullable?typescript?"|null":"?":"";

		var generics=!type.IsGenericType
			             ?[]
			             :type
			              .GetGenericArguments()
			              .Zip(
				              nullability?.GenericTypeArguments??EnumerableUtils.RepeatForever<NullabilityInfo?>(null),
				              (t,n)=>Stringify(t,typescript,input,tuplename,n))
			              .ToArray();

		if(ToStringDictionary.TryGetValue(type,out var fromDict))
			return fromDict(typescript,generics)+suffix;
		if(type.IsGenericType&&ToStringDictionary.TryGetValue(type.GetGenericTypeDefinition(),out fromDict))
			return fromDict(typescript,generics);
		foreach(var fromList in ToStringList)
			if(fromList(type,typescript,input,tuplename,nullability,generics) is{} s)
				return s+suffix;

		return typescript
			       ?$"unknwon{suffix} /*{TypeName(type,generics).Replace("/*","/#").Replace("*/","#/")}*/"
			       :$"Unknown<{TypeName(type,generics)}>{suffix}";
	}


	#region Helpers
	public static string TypeName(Type type,string[] generics){
		var name=type.Name;
		var genericIndex=name.IndexOf('`');
		if(genericIndex!=-1) name=name.Substring(0,genericIndex);
		if(generics.Length!=0) name+=$"<{generics.Join(",")}>";
		return name;
	}

	internal static string Parameter(bool typescript,string type,string? name){
		if(name==null) return type;
		if(type=="") return name;
		if(type=="null"||type[0] is '"' or '\''||double.TryParse(type,out _)) return type;//Constant value
		return typescript?$"{name}:{type}":$"{type} {name}";
	}
	#endregion

}