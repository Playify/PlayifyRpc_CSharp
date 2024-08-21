using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public static partial class DynamicTypeStringifier{
	[PublicAPI]public static readonly List<Func<State,string?>> Stringifiers=[
		DefaultStringifiers.Primitives,
		DefaultStringifiers.Nullables,
		DefaultStringifiers.Enums,
		DefaultStringifiers.Jsons,
		DefaultStringifiers.ArraysTuples,
		DefaultStringifiers.SupportedRpcTypes,
	];

	public static string FromType(Type type,bool typescript=false)=>StringifyType(new State(type,typescript));

	public static (string[] parameters,string @return) MethodSignature(Delegate method,bool typescript,params string[] prevParameters)=>MethodSignature(method.Method,typescript,prevParameters);

	public static (string[] parameters,string @return) MethodSignature(MethodInfo method,bool typescript,params string[] prevParameters)
		=>([
				  ..prevParameters,
				  ..method.GetParameters().Select(parameter=>{
					  var @params=parameter.ParameterType.IsArray&&parameter.GetCustomAttribute<ParamArrayAttribute>()!=null?typescript?"...":"params ":"";
					  var parameterType=ParameterType(parameter,true,typescript,out var state);
					  return @params+state.Parameter(parameter.Name,parameterType);
				  }),
				  // ReSharper disable once AssignNullToNotNullAttribute
			  ],ParameterType(method.ReturnParameter,false,typescript,out _));


	private static string ParameterType(ParameterInfo parameter,bool input,bool typescript,out State state){
		var tupleNames=parameter.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames;
		var tupleIndex=0;

		state=new State(parameter.ParameterType,typescript,input,()=>tupleNames?[tupleIndex++],new NullabilityInfoContext().Create(parameter));

		if(!input)
			while(true)
				if(state.Type==typeof(Task)||state.Type==typeof(ValueTask)){
					return "void";
				} else if(state.Type.IsGenericType&&(state.Type.GetGenericTypeDefinition()==typeof(Task<>)||state.Type.GetGenericTypeDefinition()==typeof(ValueTask<>))){
					state.Type=state.Type.GetGenericArguments()[0];
					state.NullabilityInfo=state.NullabilityInfo?.GenericTypeArguments[0];
				} else break;
		return StringifyType(state);
	}


	private static string StringifyType(State state){
		var nullable=(state.Input?state.NullabilityInfo?.ReadState:state.NullabilityInfo?.WriteState)==NullabilityState.Nullable?state.TypeScript?"|null":"?":"";
		foreach(var stringifier in Stringifiers)
			if(stringifier(state) is{} s)
				return s+nullable;

		//Parse is handled at the bottom, as it should be the last fallback, if nothing else works
		if(state.Input&&state.Type.GetMethod("TryParse",
			   BindingFlags.Public|BindingFlags.Static|BindingFlags.InvokeMethod,
			   null,
			   [typeof(string),state.Type.MakeByRefType()],
			   null)!=null)
			return $"string{nullable} /*{state.Type.Name}{state.Generics().Replace("/*","/#").Replace("*/","#/")}*/";


		return state.TypeScript
			       ?$"unknown{nullable} /*{state.Type.Name}{state.Generics().Replace("/*","/#").Replace("*/","#/")}*/"
			       :$"Unknown<{state.Type.Name}{state.Generics()}>{nullable}";
	}

	public struct State{
		private static readonly string[] Keywords=["return"];

		public Type Type;
		public readonly bool TypeScript;
		public readonly bool Input;
		public NullabilityInfo? NullabilityInfo;
		private readonly Func<string?>? _tupleName;

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
			if(TypeScript) return name+":"+type;
			if(Keywords.Contains(name)) name="@"+name;
			return type+" "+name;
		}

		public string Tuple(IEnumerable<string> parameters)=>TypeScript?$"[{parameters.Join(",")}]":$"({parameters.Join(",")})";


		public string SubType(Type type,NullabilityInfo? nullable)=>StringifyType(this with{Type=type,NullabilityInfo=nullable});

		public string? TupleName()=>_tupleName?.Invoke();

		public string Generics()=>Type.IsGenericType?$"<{GenericTypes().Join(",")}>":"";

		public IEnumerable<string> GenericTypes()=>Type.IsGenericType?Type.GetGenericArguments().Zip(NullabilityInfo?.GenericTypeArguments??EnumerableUtils.RepeatForever<NullabilityInfo?>(null),SubType):[];
	}


}