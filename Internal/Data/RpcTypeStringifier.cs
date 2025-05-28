using System.Reflection;
using System.Runtime.CompilerServices;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.HelperClasses;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcTypeStringifier{
	public static readonly Dictionary<Type,TypeToStringExact> ToStringDictionary=new();
	public static readonly List<TypeToStringExact> ToStringList=[];

	public delegate string TypeToString(bool typescript,string[] generics);

	public delegate string? TypeToStringExact(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics);

	static RpcTypeStringifier(){
		RpcSetupAttribute.LoadAll();
	}


	public static string FromType(Type type,bool typescript=false)=>StringifySubType(type,typescript,true,()=>null,null,null);

	public static IEnumerable<(string[] parameters,string returns)> MethodSignatures(Delegate method,ProgrammingLanguage lang=ProgrammingLanguage.CSharp,params string[] prevParameters)
		=>MethodSignatures(method.Method,lang,prevParameters);

	public static IEnumerable<(string[] parameters,string returns)> MethodSignatures(MethodInfo method,ProgrammingLanguage lang=ProgrammingLanguage.CSharp,params string[] prevParameters){
		// ReSharper disable once RedundantSuppressNullableWarningExpression
		var returns=ParameterType(method.ReturnParameter!,false,lang!=ProgrammingLanguage.CSharp);
		var list=new List<string>(prevParameters);

		foreach(var parameter in method.GetParameters()){
			if(parameter.IsOptional) yield return (list.ToArray(),returns);
			if(parameter.IsOut||parameter.ParameterType.IsByRef) yield break;//'ref' and 'out' is not supported

			if(parameter.ParameterType==typeof(FunctionCallContext)) continue;//Soem types get auto filled in
			if(parameter.ParameterType==typeof(CancellationToken)) continue;

			var @params=parameter.ParameterType.IsArray&&parameter.IsDefined(typeof(ParamArrayAttribute),true);

			if(lang==ProgrammingLanguage.JavaScript)
				list.Add($"{(@params?"...":"")}{parameter.Name}");
			else{
				var typescript=lang!=ProgrammingLanguage.CSharp;
				var parameterType=ParameterType(parameter,true,typescript);
				list.Add((@params?typescript?"...":"params ":"")+CombineParameter(typescript,parameterType,parameter.Name));
			}
		}
		yield return (list.ToArray(),returns);
	}


	private static string ParameterType(ParameterInfo parameter,bool input,bool typescript){
		var transformer=parameter.GetCustomAttribute<RpcDataTransformerAttribute>();

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

		return StringifySubType(type,typescript,input,()=>tupleNames?[tupleIndex++],nullability,transformer);
	}

	public static string StringifySubType(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,RpcDataTransformerAttribute? transformer){
		var isNullable=(input?nullability?.ReadState:nullability?.WriteState)==NullabilityState.Nullable;
		if(Nullable.GetUnderlyingType(type) is{} underlying){
			isNullable=true;
			type=underlying;
		}

		var generics=!type.IsGenericType
			             ?[]
			             :type
			              .GetGenericArguments()
			              .Zip(
				              nullability?.GenericTypeArguments??EnumerableUtils.RepeatForever<NullabilityInfo?>(null),
				              //Intentionally passing null as transformer, as a Transformer can only be applied to the top level type
				              (t,n)=>StringifySubType(t,typescript,input,tuplename,n,null))
			              .ToArray();

		var result=transformer?.StringifyType(type,typescript,input,tuplename,nullability,generics)??StringifyTypeDefault(type,typescript,input,tuplename,nullability,generics);

		if(result==null)
			return typescript
				       ?$"unknown{(isNullable?"|null":"")} /*{CombineTypeName(type,generics).Replace("/*","/#").Replace("*/","#/")}*/"
				       :$"Unknown<{CombineTypeName(type,generics)}>{(isNullable?"?":"")}";


		if(result is "null" or "void") return result;//Not nullable
		//Don't check for literals here, they could also be nullable

		if(isNullable)
			if(!typescript) result+="?";
			else if(result!="any") result+="|null";
		return result;
	}

	private static string? StringifyTypeDefault(Type type,bool typescript,bool input,Func<string?> tuplename,NullabilityInfo? nullability,string[] generics){
		if((input?RpcData.GetForInput(ToStringDictionary,type):RpcData.GetForOutput(ToStringDictionary,type)) is{} fromDict)
			return fromDict(type,typescript,input,tuplename,nullability,generics);
		foreach(var fromList in ToStringList)
			if(fromList(type,typescript,input,tuplename,nullability,generics) is{} s)
				return s;
		return null;
	}


	#region Helpers
	public static string CombineTypeName(Type type,string[] generics){
		var name=type.Name;
		var genericIndex=name.IndexOf('`');
		if(genericIndex!=-1) name=name.Substring(0,genericIndex);
		if(generics.Length!=0) name+=$"<{generics.Join(",")}>";
		return name;
	}

	internal static string CombineParameter(bool typescript,string type,string? name){
		if(name==null) return type;
		if(type=="") return name;
		if(type=="null"||type[0] is '"' or '\''||double.TryParse(type,out _)) return type;//Constant value
		return typescript?$"{name}:{type}":$"{type} {name}";
	}
	#endregion

}