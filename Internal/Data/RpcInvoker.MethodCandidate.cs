using System.Reflection;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Functions;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcInvoker{
	internal class MethodCandidate{
		public readonly MethodInfo MethodInfo;
		public readonly ParameterInfo[] AllParameters;
		public readonly ParameterInfo[] FilteredParameters;
		public readonly Type? ParamArrayType;
		public readonly int MinParameterCount;
		public readonly int MaxParameterCount;
		public RpcDataTransformerAttribute? ReturnTransformer;

		private MethodCandidate(MethodInfo methodInfo,ParameterInfo[] allParameters){
			MethodInfo=methodInfo;
			AllParameters=allParameters;
			FilteredParameters=allParameters.Where(p=>p.ParameterType!=typeof(FunctionCallContext)&&p.ParameterType!=typeof(CancellationToken)).ToArray();
			if(FilteredParameters.Length==allParameters.Length) FilteredParameters=allParameters;


			//var lastParameter=FilteredParameters[FilteredParameters.Length-1];
			ParamArrayType=FilteredParameters.LastOrDefault() is{ParameterType.IsArray: true} lastParameter&&lastParameter.IsDefined(typeof(ParamArrayAttribute))
				               ?lastParameter.ParameterType.GetElementType()
				               :null;
			if(methodInfo.CallingConvention==CallingConventions.VarArgs){
				MinParameterCount=0;
				MaxParameterCount=int.MaxValue;
			} else{
				MinParameterCount=FilteredParameters.Length;
				if(ParamArrayType!=null) MinParameterCount--;//Params array is optional, even though there is no "default value"
				while(MinParameterCount>=1&&FilteredParameters[MinParameterCount-1].HasDefaultValue)
					MinParameterCount--;
				MaxParameterCount=ParamArrayType!=null?int.MaxValue:FilteredParameters.Length;
			}

			if(MethodInfo.ReturnParameter.GetCustomAttribute<RpcDataTransformerAttribute>() is{} attribute) ReturnTransformer=attribute;
		}

		public Type ParameterType(int index){
			if(index>=FilteredParameters.Length-1&&ParamArrayType!=null) return ParamArrayType;
			return FilteredParameters[index].ParameterType;
		}

		public RpcDataTransformerAttribute? ParameterTransformer(int index){
			if(index>=FilteredParameters.Length-1&&ParamArrayType!=null) index=FilteredParameters.Length-1;
			return FilteredParameters[index].GetCustomAttribute<RpcDataTransformerAttribute>();
		}


		public object?[] FillArguments(object?[] commonArgs,RpcDataPrimitive[] args,FunctionCallContext? ctx){
			var arguments=AllParameters.Length==FilteredParameters.Length
			              &&AllParameters.Length==args.Length
				              ?commonArgs
				              :new object?[AllParameters.Length];

			var filteredIndex=0;
			for(var paramIndex=0;paramIndex<arguments.Length;paramIndex++){
				var allParameter=AllParameters[paramIndex];
				if(paramIndex==AllParameters.Length-1&&ParamArrayType!=null){
					var transformer=ParameterTransformer(paramIndex);
					var paramsArray=Array.CreateInstance(ParamArrayType,Math.Max(0,args.Length-filteredIndex));
					for(var paramsIndex=0;paramsIndex<paramsArray.Length;paramsIndex++)
						paramsArray.SetValue(
							commonArgs[filteredIndex]!=RpcData.ContinueWithNext
								?commonArgs[filteredIndex++]
								:args[filteredIndex++].To(ParamArrayType,transformer)
							,paramsIndex);
					filteredIndex=args.Length;
					arguments[paramIndex]=paramsArray;
				} else if(filteredIndex>=FilteredParameters.Length||allParameter!=FilteredParameters[filteredIndex]){
					//Parameter was filtered out beforehand
					//check with index>=length is needed when the last argument is FunctionCallContext, as otherwise it would cause out of bounds on filtered parameters
					if(allParameter.ParameterType==typeof(FunctionCallContext)) arguments[paramIndex]=ctx;
					else if(allParameter.ParameterType==typeof(CancellationToken)) arguments[paramIndex]=ctx?.CancellationToken??default(CancellationToken);
					else arguments[paramIndex]=null;
				} else if(filteredIndex<args.Length){
					arguments[paramIndex]=
						commonArgs[filteredIndex]!=RpcData.ContinueWithNext
							?commonArgs[filteredIndex++]
							:args[filteredIndex++].To(allParameter.ParameterType,ParameterTransformer(paramIndex));
				} else{
					filteredIndex++;
					arguments[paramIndex]=allParameter.DefaultValue;
				}
				//maybe somewhere in here, there should be a check for method.CallingConvention&CallingConventions.VarArgs
			}


			return arguments;
		}


		public static MethodCandidate? Create(MethodInfo info){
			var allParameters=info.GetParameters();
			if(allParameters.Any(p=>p.IsOut||p.ParameterType.IsByRef)) return null;
			return new MethodCandidate(info,allParameters);
		}
	}
}