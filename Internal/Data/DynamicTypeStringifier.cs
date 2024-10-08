using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public static partial class DynamicTypeStringifier{
	[PublicAPI]public static readonly List<Func<State,string?>> Stringifiers=[
		DefaultStringifiers.Primitives,
		DefaultStringifiers.Enums,
		DefaultStringifiers.Jsons,
		DefaultStringifiers.ArraysTuples,
		DefaultStringifiers.SupportedRpcTypes,
	];

	public static string FromType(Type type,bool typescript=false)=>StringifyType(new State(type,typescript));

	public static IEnumerable<(string[] parameters,string returns)> MethodSignatures(Delegate method,bool typescript,params string[] prevParameters)=>MethodSignatures(method.Method,typescript,prevParameters);

	public static IEnumerable<(string[] parameters,string returns)> MethodSignatures(MethodInfo method,bool typescript,params string[] prevParameters){
		var returns=ParameterType(method.ReturnParameter,false,typescript,out _);
		var list=new List<string>(prevParameters);

		foreach(var parameter in method.GetParameters()){
			if(parameter.IsOptional) yield return (list.ToArray(),returns);

			var @params=parameter.ParameterType.IsArray&&parameter.GetCustomAttribute<ParamArrayAttribute>()!=null?typescript?"...":"params ":"";
			var parameterType=ParameterType(parameter,true,typescript,out var state);
			list.Add(@params+state.Parameter(parameter.Name,parameterType));
		}
		yield return (list.ToArray(),returns);
	}


	private static string ParameterType(ParameterInfo parameter,bool input,bool typescript,out State state){
		var tupleNames=parameter.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames;
		var tupleIndex=0;

		state=new State(parameter.ParameterType,typescript,input,()=>tupleNames?[tupleIndex++],new NullabilityInfoContext().Create(parameter));

		if(!input)
			while(true)
				if(state.Type==typeof(void)||state.Type==typeof(Task)||state.Type==typeof(ValueTask)){
					return "void";
				} else if(state.Type.IsGenericType&&(state.Type.GetGenericTypeDefinition()==typeof(Task<>)||state.Type.GetGenericTypeDefinition()==typeof(ValueTask<>))){
					state.Type=state.Type.GetGenericArguments()[0];
					state.NullabilityInfo=state.NullabilityInfo?.GenericTypeArguments[0];
				} else break;
		return StringifyType(state);
	}


	private static string StringifyType(State state){
		var isNullable=(state.Input?state.NullabilityInfo?.ReadState:state.NullabilityInfo?.WriteState)==NullabilityState.Nullable;
		if(Nullable.GetUnderlyingType(state.Type) is{} underlying){
			isNullable=true;
			state.Type=underlying;
		}
		var nullable=isNullable?state.TypeScript?"|null":"?":"";
		foreach(var stringifier in Stringifiers)
			if(stringifier(state) is{} s)
				return s+nullable;

		//Parse is handled at the bottom, as it should be the last fallback, if nothing else works
		if(state.Input&&state.Type.GetMethod("TryParse",
			   BindingFlags.Public|BindingFlags.Static|BindingFlags.InvokeMethod,
			   null,
			   [typeof(string),state.Type.MakeByRefType()],
			   null)!=null)
			return $"string{nullable} /*{state.TypeName}{state.Generics().Replace("/*","/#").Replace("*/","#/")}*/";


		return state.TypeScript
			       ?$"unknown{nullable} /*{state.TypeName}{state.Generics().Replace("/*","/#").Replace("*/","#/")}*/"
			       :$"Unknown<{state.TypeName}{state.Generics()}>{nullable}";
	}

	public struct State{
		private static readonly string[] Keywords=["return"];

		public Type Type;
		public readonly bool TypeScript;
		public readonly bool Input;
		public NullabilityInfo? NullabilityInfo;
		private readonly Func<string?>? _tupleName;
		public string TypeName{
			get{
				var s=Type.Name;
				var i=s.IndexOf('`');
				return i==-1?s:s.Substring(0,i);
			}
		}

		public State(Type type,bool typeScript,bool input,Func<string?>? tupleName,NullabilityInfo nullabilityInfo){
			_tupleName=tupleName;
			Type=type;
			TypeScript=typeScript;
			Input=input;
			NullabilityInfo=nullabilityInfo;
		}

		public State(Type type,bool typeScript){
			Type=type;
			TypeScript=typeScript;
			_tupleName=null;
			Input=true;
			NullabilityInfo=null;
		}

		public string Parameter(string? name,string type){
			if(name==null) return type;
			if(Keywords.Contains(name)) name=(TypeScript?"_":"@")+name;
			if(TypeScript) return name+":"+type;
			return type+" "+name;
		}

		public string Tuple(IEnumerable<string> parameters)=>TypeScript?$"[{parameters.Join(",")}]":$"({parameters.Join(",")})";


		public string SubType(Type type,NullabilityInfo? nullable)=>StringifyType(this with{Type=type,NullabilityInfo=nullable});

		public string? TupleName()=>_tupleName?.Invoke();

		public string Generics()=>Type.IsGenericType?$"<{GenericTypes().Join(",")}>":"";

		public IEnumerable<string> GenericTypes()=>Type.IsGenericType?Type.GetGenericArguments().Zip(NullabilityInfo?.GenericTypeArguments??EnumerableUtils.RepeatForever<NullabilityInfo?>(null),SubType):[];
	}
}