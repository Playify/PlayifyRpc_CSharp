using System.Reflection;
using JetBrains.Annotations;
using PlayifyRpc.Types.Exceptions;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public static partial class RpcInvoker{

	public static object? InvokeMethod(Delegate func,string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx)
		=>InvokeMethod(func.Target,[func.Method],type,method,args,ctx);

	public static object? InvokeMethod(object? instance,IList<MethodInfo> overloads,string? type,string method,RpcDataPrimitive[] args,FunctionCallContext ctx)
		=>InvokeThrow(instance,overloads,args,msg=>new RpcMethodNotFoundException(type,method,msg),ctx);

	internal static object? InvokeMeta(Delegate func,string? type,string meta,RpcDataPrimitive[] args,FunctionCallContext ctx)
		=>InvokeThrow(func.Target,[func.Method],args,msg=>new RpcMetaMethodNotFoundException(type,meta,msg),ctx);

	public static object? InvokeThrow(Delegate func,RpcDataPrimitive[] args)=>InvokeThrow(func.Target,[func.Method],args,msg=>new RpcException(null,null,msg,""),null);

	public static object? InvokeThrow(object? instance,IList<MethodInfo> overloads,RpcDataPrimitive[] args,Func<string,RpcException> error,FunctionCallContext? ctx){
		try{
			if(overloads.Count==0) throw error("No overload found at all");

			var candidates=new ParameterInfo[]?[overloads.Count];
			for(var i=0;i<overloads.Count;i++){
				var parameters=overloads[i].GetParameters();
				if(parameters.Any(p=>p.IsOut||p.ParameterType.IsByRef)) continue;//No viable overload, 'ref' and 'out' is not supported

				if(ctx!=null){//Filter out FunctionCallContext parameter, it gets filled in later but for now it is in the way
					var fccIndex=Array.FindIndex(parameters,p=>p.ParameterType==typeof(FunctionCallContext));
					if(fccIndex!=-1){
						var newParameters=new ParameterInfo[parameters.Length-1];
						Array.Copy(parameters,newParameters,fccIndex);
						Array.Copy(parameters,fccIndex+1,newParameters,fccIndex,parameters.Length-fccIndex-1);
						parameters=newParameters;
					}
				}

				candidates[i]=parameters;
			}
			if(candidates.All(c=>c==null)) throw error("No viable overload found");

			var paramArrayTypes=new Type?[candidates.Length];

			//Check based on argument length
			for(var i=0;i<candidates.Length;i++)
				if(candidates[i] is{} candidate&&!FilterByArgLength(overloads[i],candidate,args,out paramArrayTypes[i]))
					candidates[i]=null;
			if(candidates.All(c=>c==null)) throw error("Method doesn't accept "+args.Length+" arguments");


			//Check parameters one by one
			var typesOfCurrentArg=new Type?[candidates.Length];
			for(var argIndex=0;argIndex<args.Length;argIndex++){
				//get all current args of all candidates
				for(var candidateIndex=0;candidateIndex<candidates.Length;candidateIndex++)
					typesOfCurrentArg[candidateIndex]=
						candidates[candidateIndex] is not{} candidate
							?null
							:argIndex>=candidate.Length-1&&paramArrayTypes[candidateIndex] is{} paramArray
								?paramArray
								:candidate[argIndex].ParameterType;


				var isCommonType=true;
				Type? commonType=null;
				for(var candidateIndex=0;candidateIndex<candidates.Length;candidateIndex++)
					if(commonType==null) commonType=typesOfCurrentArg[candidateIndex];
					else if(typesOfCurrentArg[candidateIndex] is{} currType&&currType!=commonType)
						isCommonType=false;

				if(isCommonType){
					if(commonType==null) throw error("Error casting arguments");

					try{
						args[argIndex].TryTo(commonType,out _,true);
					} catch(RpcDataException e){
						e.Data["argIndex"]=argIndex;
						throw;
					}
				} else{
					for(var candidateIndex=0;candidateIndex<candidates.Length;candidateIndex++)
						if(typesOfCurrentArg[candidateIndex] is{} type&&!args[argIndex].TryTo(type,out _))
							candidates[candidateIndex]=null;
					if(candidates.All(c=>c==null)){
						var e=error("Method doesn't accept those argument types");
						e.Data["argIndex"]=argIndex;
						throw e;
					}
				}
			}

			var currentMin=Array.FindIndex(candidates,c=>c!=null);

			var ambiguous=false;
			var old=candidates[currentMin]!;
			for(var i=currentMin+1;i<candidates.Length;i++)
				if(candidates[i] is{} @new)
					//Walk all methods looking for the most specific one
					if(!FindMostSpecificMethod(
						   overloads[currentMin],old,paramArrayTypes[currentMin],
						   overloads[i],@new,paramArrayTypes[i],
						   args.Length
					   ).TryGet(out var isMoreSpecific)) ambiguous=true;
					else if(isMoreSpecific){
						currentMin=i;
						old=@new;
						ambiguous=false;
					}
			if(ambiguous){
				var e=error("Call is ambiguous");
				e.Data["ambiguous"]=true;
				throw e;
			}


			//Best candidate found, read all args and invoke

			var method=overloads[currentMin];
			var paramsArrayType=paramArrayTypes[currentMin];

			var allParameters=method.GetParameters();
			var arguments=new object?[allParameters.Length];
			var rpcArgIndex=0;

			for(var i=0;i<allParameters.Length;i++){
				var allParam=allParameters[i];
				if(i==allParameters.Length-1&&paramsArrayType!=null){
					var paramsArray=Array.CreateInstance(paramsArrayType,Math.Max(0,args.Length-rpcArgIndex));
					for(var paramsIndex=0;paramsIndex<paramsArray.Length;paramsIndex++)
						paramsArray.SetValue(args[rpcArgIndex++].To(paramsArrayType),paramsIndex);
					rpcArgIndex=args.Length;
					arguments[i]=paramsArray;
				} else if(old.Length<=rpcArgIndex||allParam!=old[rpcArgIndex]){//Parameter was filtered out beforehand, fill it with FunctionCallContext
					arguments[i]=ctx;
				} else if(rpcArgIndex<args.Length){
					arguments[i]=args[rpcArgIndex++].To(allParam.ParameterType);
				} else{
					rpcArgIndex++;
					arguments[i]=allParam.DefaultValue;
				}
				//maybe somewhere in here, there should be a check for method.CallingConvention&CallingConventions.VarArgs
			}

			if(ctx!=null&&//If ctx==null, then the IDE already showed the user that a function is obsolete
			   method.GetCustomAttribute<ObsoleteAttribute>() is{} obsolete)
				Task.Run(async ()=>{
					string? caller=null;
					try{
						caller=await ctx.GetCaller();
					} catch(Exception){/*ignored*/
					}
					if(caller==Rpc.PrettyName) return;//If called locally, then ignore it, IDE already should have warned
					Rpc.Logger.Warning($"{caller??"<<someone>>"} is calling {
						ctx.Type??"<<null>>"}.{ctx.Method??"<<null>>"} which is obsolete{
							(obsolete.Message==null?".":": "+obsolete.Message)}");
				});

			return method.Invoke(instance,arguments);
		} catch(TargetInvocationException e){
			throw RpcException.WrapAndFreeze(e.InnerException??e);
		} catch(Exception e){
			throw RpcException.WrapAndFreeze(e);
		}
	}
}