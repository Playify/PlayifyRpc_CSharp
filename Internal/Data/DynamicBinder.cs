using System.Globalization;
using System.Reflection;
using PlayifyRpc.Types.Exceptions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public partial class DynamicBinder:Binder{
	private static readonly ThreadLocal<MethodInfo?> CurrentMethod=new();
	private static DynamicBinder? _instance;
	public static DynamicBinder Instance=>_instance??=new DynamicBinder();

	private DynamicBinder(){
	}

	//Intentionally throws MethodAccessException instead of MethodMissingException
	public sealed override MethodBase BindToMethod(BindingFlags bindingAttr,MethodBase[] match,ref object?[] args,
		ParameterModifier[]? modifiers,CultureInfo? cultureInfo,string[]? names,out object? state){
		if(names!=null) throw new NotSupportedException("Named arguments are not supported");
		if(match==null||match.Length==0) throw new ArgumentException(nameof(match));

		if(CurrentMethod.Value is{} only){
			match=match.Contains(only)?[only]:[];
			CurrentMethod.Value=null;
		}

		state=null;

		//Create candidates
		var candidates=CreateCandidates(match);
		if(candidates.All(c=>!c.HasValue)) throw new MethodAccessException("Method not found");


		//Create cache for arg types
		var paramArrayTypes=new Type?[candidates.Length];

		var argTypes=new Type?[args.Length];
		for(var i=0;i<args.Length;i++)
			argTypes[i]=args[i]?.GetType();


		//Filter by arg length
		for(var canIndex=0;canIndex<candidates.Length;canIndex++){
			if(!candidates[canIndex].TryGet(out var candidate)) continue;
			var (method,par)=candidate;
			if(!FilterByArgLength(method,par,args,argTypes,out paramArrayTypes[canIndex]))
				candidates[canIndex]=null;
		}
		if(candidates.All(c=>!c.HasValue)) throw new MethodAccessException("Method doesn't accept "+args.Length+" arguments");


		//Filter by arg types
		var candidateArgTypes=new Type?[candidates.Length];
		for(var argIndex=0;argIndex<args.Length;argIndex++){
			//Get all arg types
			for(var canIndex=0;canIndex<candidates.Length;canIndex++)
				if(!candidates[canIndex].TryGet(out var candidate)) candidateArgTypes[canIndex]=null;
				else candidateArgTypes[canIndex]=GetNthArgType(bindingAttr,candidate.par,args,paramArrayTypes[canIndex],argIndex);

			Type? nThCommonType=null;
			for(var canIndex=0;canIndex<candidates.Length;canIndex++)
				if(nThCommonType==null) nThCommonType=candidateArgTypes[canIndex];
				else if(candidateArgTypes[canIndex]==null){
				} else if(nThCommonType!=candidateArgTypes[canIndex]){//Not common, each candidate has to be checked separately

					for(canIndex=0;canIndex<candidates.Length;canIndex++){
						if(candidateArgTypes[canIndex] is not{} candidateArgType) continue;
						var value=args[argIndex];
						if(!DynamicCaster.TryCast(value,candidateArgType,out _))
							candidates[canIndex]=null;
					}

					nThCommonType=null;
					break;
				}

			if(nThCommonType==null) continue;//Type will be accepted, so next arg can be handled

			try{
				//Try casting the corresponding arg to the type, and throw if not correct
				DynamicCaster.Cast(args[argIndex],nThCommonType);
				/*don't use 'args[argIndex]=' here, as it should also work for the TryCast variant, that can't use that*/
			} catch(RpcDataException e){
				e.Data["argIndex"]=argIndex;
				throw;
			}
		}
		var currentMin=Array.FindIndex(candidates,c=>c!=null);
		if(currentMin==-1) throw new MethodAccessException("Method doesn't accept those argument types");


		var ambiguous=false;
		for(var canIndex=currentMin+1;canIndex<candidates.Length;canIndex++){
			if(!candidates[canIndex].TryGet(out var newTuple)||
			   !candidates[currentMin].TryGet(out var oldTuple))
				continue;

			#region Walk all of the methods looking the most specific method to invoke
			var newMin=FindMostSpecificMethod(oldTuple,
				paramArrayTypes[currentMin],
				newTuple,
				paramArrayTypes[canIndex],
				argTypes,
				args);

			if(newMin==0) ambiguous=true;
			else if(newMin==2){
				currentMin=canIndex;
				ambiguous=false;
			}
			#endregion

		}

		if(ambiguous) throw new AmbiguousMatchException();

		{
			var (method,par)=candidates[currentMin]!.Value;
			var paramArrayType=paramArrayTypes[currentMin];
			CorrectArgs(method,par,ref args,paramArrayType);
			return method;
		}
	}

	public override object ChangeType(object value,Type type,CultureInfo? cultureInfo)=>DynamicCaster.Cast(value,type)!;

	public override FieldInfo BindToField(BindingFlags bindingAttr,FieldInfo[] match,
		object value,CultureInfo? cultureInfo)
		=>throw new NotSupportedException();

	public override MethodBase SelectMethod(BindingFlags bindingAttr,MethodBase[] match,
		Type[] types,ParameterModifier[]? modifiers)
		=>throw new NotSupportedException();

	public override PropertyInfo SelectProperty(BindingFlags bindingAttr,PropertyInfo[] match,
		Type? returnType,Type[]? indexes,ParameterModifier[]? modifiers)
		=>throw new NotSupportedException();


	public override void ReorderArgumentArray(ref object?[] args,object state){
	}
}