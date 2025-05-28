using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public static partial class RpcInvoker{

	private static readonly Type VoidTaskResult=Type.GetType("System.Threading.Tasks.VoidTaskResult")!;

	internal static Task<RpcDataPrimitive> InvokeThrow(object? instance,IList<MethodCandidate?> overloads,RpcDataPrimitive[] args,Func<string,RpcException> error,FunctionCallContext? ctx){
		try{
			if(overloads.Count==0) throw error("No viable overload found");

			var candidates=overloads.ToArray();

			var argCount=args.Length;
			for(var i=0;i<candidates.Length;i++)
				if(candidates[i] is{} candidate&&(argCount<candidate.MinParameterCount||argCount>candidate.MaxParameterCount))
					candidates[i]=null;
			if(candidates.All(c=>c==null)) throw error("Method doesn't accept "+argCount+" arguments");


			//Check parameters one by one
			var commonArgs=new object?[argCount];
			{
				var typesOfCurrentArg=new Type?[candidates.Length];
				var transformerOfCurrentArg=new RpcDataTransformerAttribute?[candidates.Length];
				for(var argIndex=0;argIndex<argCount;argIndex++){
					for(var i=0;i<typesOfCurrentArg.Length;i++){
						typesOfCurrentArg[i]=candidates[i]?.ParameterType(argIndex);
						transformerOfCurrentArg[i]=candidates[i]?.ParameterTransformer(argIndex);
					}


					Type? commonType=null;
					RpcDataTransformerAttribute? commonTransformer=null;
					for(var candidateIndex=0;candidateIndex<candidates.Length;candidateIndex++)
						if(commonType==null){
							commonType=typesOfCurrentArg[candidateIndex];
							commonTransformer=transformerOfCurrentArg[candidateIndex];
						} else if(typesOfCurrentArg[candidateIndex] is{} currType&&
						          (currType!=commonType||commonTransformer!=transformerOfCurrentArg[candidateIndex])){
							commonType=null;
							commonTransformer=null;
							break;
						}

					if(commonType!=null){
						try{
							args[argIndex].TryTo(commonType,out commonArgs[argIndex],true,commonTransformer);
						} catch(RpcDataException e){
							e.Data["argIndex"]=argIndex;
							throw;
						}
					} else{
						for(var candidateIndex=0;candidateIndex<candidates.Length;candidateIndex++)
							if(typesOfCurrentArg[candidateIndex] is{} type){
								if(!args[argIndex].TryTo(type,out var val,false,transformerOfCurrentArg[candidateIndex]))
									candidates[candidateIndex]=null;
								else commonArgs[argIndex]=val;//Reuse if possible. only happens when this candidate will be the only one remaining after this loop
							}//else commonArgs[argIndex]= no need to change, as this candidate is null, so it doesn't add anything

						var count=candidates.Count(c=>c!=null);
						if(count==0){
							var e=error("Method doesn't accept those argument types");
							e.Data["argIndex"]=argIndex;
							throw e;
						}
						if(count!=1)
							commonArgs[argIndex]=RpcData.ContinueWithNext;
					}
				}
			}


			//Find best candidate
			MethodCandidate bestCandidate;
			{
				var currentMin=Array.FindIndex(candidates,c=>c!=null);
				if(currentMin<0) throw error("Method arguments could not be parsed properly");//This should normally already be caught previously
				bestCandidate=candidates[currentMin]!;
				var ambiguous=false;
				for(var i=currentMin+1;i<candidates.Length;i++)
					if(candidates[i] is{} newCandidate)
						//Walk all methods looking for the most specific one
						if(!FindMostSpecificMethod(bestCandidate,newCandidate).TryGet(out var isMoreSpecific)) ambiguous=true;
						else if(isMoreSpecific){
							bestCandidate=newCandidate;
							ambiguous=false;
						}
				if(ambiguous){
					var e=error("Call is ambiguous");
					e.Data["ambiguous"]=true;
					throw e;
				}
			}

			//Invoke
			var result=bestCandidate.MethodInfo.Invoke(instance,bestCandidate.FillArguments(commonArgs,args,ctx));
			return ObjectToTask(result,bestCandidate.ReturnTransformer);
		} catch(TargetInvocationException e){
			throw RpcException.WrapAndFreeze(e.InnerException??e);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e);
		}
	}

	internal static async Task<RpcDataPrimitive> ObjectToTask(object? result,RpcDataTransformerAttribute? transformer){
		while(true)
			switch(result){
				case Task task:
					await task;
					result=task.GetType().GetProperty(nameof(Task<object>.Result))?.GetValue(result);
					if(VoidTaskResult.IsInstanceOfType(result)) result=null;//VoidTaskResult should only occur here, and not elsewhere
					continue;
				case ValueTask valueTask:
					await valueTask;
					result=null;
					continue;
				case not null when result.GetType() is{IsGenericType: true} t&&t.GetGenericTypeDefinition()==typeof(ValueTask<>):
					result=t.GetMethod(nameof(ValueTask<object>.AsTask))?.Invoke(result,[]);
					continue;
				default:
					return RpcDataPrimitive.From(result,null,transformer);
			}
	}
}