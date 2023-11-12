using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using PlayifyRpc.Internal;

namespace PlayifyRpc.Types.Data;

public class DynamicBinder:Binder{
	private static DynamicBinder? _instance;
	public static DynamicBinder Instance=>_instance??=new DynamicBinder();

	private DynamicBinder(){
	}

	// This method is passed a set of methods and must choose the best
	// fit.  The methods all have the same number of arguments and the object
	// array args.  On exit, this method will choice the best fit method
	// and coerce the args to match that method.  By match, we mean all primitive
	// arguments are exact matches and all object arguments are exact or subclasses
	// of the target.  If the target OR is an interface, the object must implement
	// that interface.  There are a couple of exceptions
	// thrown when a method cannot be returned.  If no method matches the args an
	// ArgumentException is thrown.  If multiple methods match the args then
	// an AmbiguousMatchException is thrown.
	//
	// The most specific match will be selected.
	//
	public sealed override MethodBase BindToMethod(BindingFlags bindingAttr,MethodBase[] match,ref object?[] args,
	                                               ParameterModifier[]? modifiers,CultureInfo? cultureInfo,string[]? names,out object? state){
		if(match==null||match.Length==0) throw new ArgumentException(nameof(match));

		MethodBase?[] candidates=(MethodBase[])match.Clone();
		

		int i;
		int j;

		state=null;

		#region Map named parameters to candidate parameter positions
		// We are creating an paramOrder array to act as a mapping
		//  between the order of the args and the actual order of the
		//  parameters in the method.  This order may differ because
		//  named parameters (names) may change the order.  If names
		//  is not provided, then we assume the default mapping (0,1,...)
		var paramOrder=new int[candidates.Length][];

		for(i=0;i<candidates.Length;i++){
			var par=candidates[i]!.GetParameters();

			// args.Length + 1 takes into account the possibility of a last paramArray that can be omitted
			paramOrder[i]=new int[(par.Length>args.Length)?par.Length:args.Length];

			if(names==null){
				// Default mapping
				for(j=0;j<args.Length;j++) paramOrder[i][j]=j;
			} else{
				// Named parameters, reorder the mapping.  If CreateParamOrder fails, it means that the method
				// doesn't have a name that matches one of the named parameters so we don't consider it any further.
				if(!CreateParamOrder(paramOrder[i],par,names)) candidates[i]=null;
			}
		}
		#endregion

		var paramArrayTypes=new Type?[candidates.Length];

		var argTypes=new Type?[args.Length];

		#region Cache the type of the provided arguments
		// object that contain a null are treated as if they were typeless (but match either object
		// references or value classes).  We mark this condition by placing a null in the argTypes array.
		for(i=0;i<args.Length;i++){
			if(args[i]!=null){
				argTypes[i]=args[i]!.GetType();
			}
		}
		#endregion

		// Find the method that matches...
		var curIdx=0;
		var defaultValueBinding=((bindingAttr&BindingFlags.OptionalParamBinding)!=0);

		#region Filter methods by parameter count and type
		for(i=0;i<candidates.Length;i++){
			Type? paramArrayType=null;

			// If we have named parameters then we may have a hole in the candidates array.
			if(candidates[i]==null) continue;

			// Validate the parameters.
			var par=candidates[i]!.GetParameters();

			#region Match method by parameter count
			if(par.Length==0){

				#region No formal parameters
				if(args.Length!=0){
					if((candidates[i]!.CallingConvention&CallingConventions.VarArgs)==0) continue;
				}

				// This is a valid routine so we move it up the candidates list.
				paramOrder[curIdx]=paramOrder[i];
				candidates[curIdx++]=candidates[i];

				continue;
				#endregion

			} else if(par.Length>args.Length){

				#region Shortage of provided parameters
				// If the number of parameters is greater than the number of args then
				// we are in the situation were we may be using default values.
				for(j=args.Length;j<par.Length-1;j++){
					if(par[j].DefaultValue==DBNull.Value) break;
				}

				if(j!=par.Length-1) continue;

				if(par[j].DefaultValue==DBNull.Value){
					if(!par[j].ParameterType.IsArray) continue;

					if(!par[j].IsDefined(typeof(ParamArrayAttribute),true)) continue;

					paramArrayType=par[j].ParameterType.GetElementType();
				}
				#endregion

			} else if(par.Length<args.Length){

				#region Excess provided parameters
				// test for the ParamArray case
				var lastArgPos=par.Length-1;

				if(!par[lastArgPos].ParameterType.IsArray) continue;

				if(!par[lastArgPos].IsDefined(typeof(ParamArrayAttribute),true)) continue;

				if(paramOrder[i][lastArgPos]!=lastArgPos) continue;

				paramArrayType=par[lastArgPos].ParameterType.GetElementType();
				#endregion

			} else{

				#region Test for paramArray, save paramArray type
				var lastArgPos=par.Length-1;

				if(par[lastArgPos].ParameterType.IsArray
				   &&par[lastArgPos].IsDefined(typeof(ParamArrayAttribute),true)
				   &&paramOrder[i][lastArgPos]==lastArgPos){
					if(!par[lastArgPos].ParameterType.IsAssignableFrom(argTypes[lastArgPos])) paramArrayType=par[lastArgPos].ParameterType.GetElementType();
				}
				#endregion

			}
			#endregion

			var argsToCheck=(paramArrayType!=null)?par.Length-1:args.Length;

			#region Match method by parameter type
			for(j=0;j<argsToCheck;j++){

				#region Classic argument coercion checks
				// get the formal type
				var pCls=par[j].ParameterType;

				if(pCls.IsByRef) pCls=pCls.GetElementType()!;

				// the type is the same
				var argType=argTypes[paramOrder[i][j]];
				if(pCls==argType) continue;

				// a default value is available
				if(defaultValueBinding&&args[paramOrder[i][j]]==Type.Missing) continue;

				// the argument was null, so it matches with everything
				if(args[paramOrder[i][j]]==null) continue;

				// the type is Object, so it will match everything
				if(pCls==typeof(object)) continue;

				// now do a "classic" type check

				if(!StaticallyTypedUtils.TryCast(args[paramOrder[i][j]],pCls,out _)) break;
				#endregion

			}

			if(paramArrayType!=null&&j==par.Length-1){

				#region Check that excess arguments can be placed in the param array
				for(;j<args.Length;j++){
					if(!StaticallyTypedUtils.TryCast(args[j],paramArrayType,out _)) break;
				}
				#endregion

			}
			#endregion

			if(j==args.Length){

				#region This is a valid routine so we move it up the candidates list
				paramOrder[curIdx]=paramOrder[i];
				paramArrayTypes[curIdx]=paramArrayType!;
				candidates[curIdx++]=candidates[i];
				#endregion

			}
		}
		#endregion

		// If we didn't find a method
		if(curIdx==0) throw new MissingMethodException();

		if(curIdx==1){

			#region Found only one method
			var paramArrayType=paramArrayTypes[0];
			if(names!=null){
				state=new BinderState((int[])paramOrder[0].Clone(),args.Length,paramArrayType!=null);
				ReorderParams(paramOrder[0],args);
			}

			// If the parameters and the args are not the same length or there is a paramArray
			//  then we need to create a argument array.
			var candidateParameters=candidates[0]!.GetParameters();

			if(candidateParameters.Length==args.Length){
				if(paramArrayType!=null){
					var objs=new object[candidateParameters.Length];
					var lastPos=candidateParameters.Length-1;
					Array.Copy(args,objs,lastPos);
					objs[lastPos]=Array.CreateInstance(paramArrayType,1);
					((Array)objs[lastPos]).SetValue(args[lastPos],0);
					args=objs;
				}
			} else if(candidateParameters.Length>args.Length){
				object?[] objs=new object[candidateParameters.Length];

				for(i=0;i<args.Length;i++) objs[i]=args[i];

				for(;i<candidateParameters.Length-1;i++) objs[i]=candidateParameters[i].DefaultValue;

				if(paramArrayType!=null) objs[i]=Array.CreateInstance(paramArrayType,0);// create an empty array for the

				else objs[i]=candidateParameters[i].DefaultValue;

				args=objs;
			} else{
				if((candidates[0]!.CallingConvention&CallingConventions.VarArgs)==0){
					var objs=new object[candidateParameters.Length];
					var paramArrayPos=candidateParameters.Length-1;
					Array.Copy(args,objs,paramArrayPos);
					objs[paramArrayPos]=Array.CreateInstance(paramArrayType??throw new NullReferenceException(),args.Length-paramArrayPos);
					Array.Copy(args,paramArrayPos,(Array)objs[paramArrayPos],0,args.Length-paramArrayPos);
					args=objs;
				}
			}
			#endregion

			return candidates[0]!;
		}

		var currentMin=0;
		var ambiguous=false;
		var paramArrayType1=paramArrayTypes[currentMin];
		for(i=1;i<curIdx;i++){

			#region Walk all of the methods looking the most specific method to invoke
			var newMin=FindMostSpecificMethod(candidates[currentMin]!,paramOrder[currentMin],paramArrayType1,
			                                  candidates[i]!,paramOrder[i],paramArrayTypes[i],argTypes,args);

			if(newMin==0){
				ambiguous=true;
			} else if(newMin==2){
				currentMin=i;
				ambiguous=false;
			}
			#endregion

		}

		if(ambiguous) throw new AmbiguousMatchException();

		// Reorder (if needed)
		if(names!=null){
			state=new BinderState((int[])paramOrder[currentMin].Clone(),args.Length,paramArrayType1!=null);
			ReorderParams(paramOrder[currentMin],args);
		}

		// If the parameters and the args are not the same length or there is a paramArray
		//  then we need to create a argument array.
		var parameters=candidates[currentMin]!.GetParameters();
		if(parameters.Length==args.Length){
			if(paramArrayType1!=null){
				var objs=new object[parameters.Length];
				var lastPos=parameters.Length-1;
				Array.Copy(args,objs,lastPos);
				objs[lastPos]=Array.CreateInstance(paramArrayType1,1);
				((Array)objs[lastPos]).SetValue(args[lastPos],0);
				args=objs;
			}
		} else if(parameters.Length>args.Length){
			object?[] objs=new object[parameters.Length];

			for(i=0;i<args.Length;i++) objs[i]=args[i];

			for(;i<parameters.Length-1;i++) objs[i]=parameters[i].DefaultValue;

			if(paramArrayType1!=null){
				objs[i]=Array.CreateInstance(paramArrayType1,0);
			} else{
				objs[i]=parameters[i].DefaultValue;
			}

			args=objs;
		} else{
			if((candidates[currentMin]!.CallingConvention&CallingConventions.VarArgs)==0){
				var objs=new object[parameters.Length];
				var paramArrayPos=parameters.Length-1;
				Array.Copy(args,objs,paramArrayPos);
				objs[paramArrayPos]=Array.CreateInstance(paramArrayType1??throw new NullReferenceException(),args.Length-paramArrayPos);
				Array.Copy(args,paramArrayPos,(Array)objs[paramArrayPos],0,args.Length-paramArrayPos);
				args=objs;
			}
		}

		return candidates[currentMin]!;
	}

	// Given a set of fields that match the base criteria, select a field.
	// if value is null then we have no way to select a field
	public sealed override FieldInfo BindToField(BindingFlags bindingAttr,FieldInfo[] match,object value,CultureInfo? cultureInfo){
		if(match==null){
			throw new ArgumentNullException(nameof(match));
		}

		int i;
		// Find the method that match...
		var curIdx=0;

		var candidates=(FieldInfo[])match.Clone();

		// If we are a FieldSet, then use the value's type to disambiguate
		if((bindingAttr&BindingFlags.SetField)!=0){
			var valueType=value.GetType();

			for(i=0;i<candidates.Length;i++){
				var pCls=candidates[i].FieldType;
				if(pCls==valueType){
					candidates[curIdx++]=candidates[i];
					continue;
				}
				if(value==null){
					// the object passed in was null which would match any non primitive non value type
					if(pCls.IsClass){
						candidates[curIdx++]=candidates[i];
						continue;
					}
				}
				if(pCls==typeof(object)){
					candidates[curIdx++]=candidates[i];
					continue;
				}

				if(StaticallyTypedUtils.TryCast(value,pCls,out _)){
					candidates[curIdx++]=candidates[i];
				}
			}
			if(curIdx==0) throw new MissingFieldException();
			if(curIdx==1) return candidates[0];
		}

		// Walk all of the methods looking the most specific method to invoke
		var currentMin=0;
		var ambiguous=false;
		for(i=1;i<curIdx;i++){
			var newMin=FindMostSpecificField(candidates[currentMin],candidates[i]);
			if(newMin==0) ambiguous=true;
			else{
				if(newMin==2){
					currentMin=i;
					ambiguous=false;
				}
			}
		}
		if(ambiguous) throw new AmbiguousMatchException();
		return candidates[currentMin];
	}

	// Given a set of methods that match the base criteria, select a method based
	// upon an array of types.  This method should return null if no method matches
	// the criteria.
	public sealed override MethodBase? SelectMethod(BindingFlags bindingAttr,MethodBase[] match,Type[] types,ParameterModifier[]? modifiers){

		var realTypes=new Type[types.Length];
		for(var i=0;i<types.Length;i++){
			realTypes[i]=types[i].UnderlyingSystemType;
			//if(!(realTypes[i].IsRuntimeImplemented()||realTypes[i] is SignatureType)) throw new ArgumentException(SR.Arg_MustBeType,nameof(types));
		}
		types=realTypes;

		// We don't automatically jump out on exact match.
		if(match==null||match.Length==0) throw new ArgumentException(nameof(match));

		var candidates=(MethodBase[])match.Clone();

		// Find all the methods that can be described by the types parameter.
		//  Remove all of them that cannot.
		var curIdx=0;
		for(var i=0;i<candidates.Length;i++){
			var par=candidates[i].GetParameters();
			if(par.Length!=types.Length) continue;
			int j;
			for(j=0;j<types.Length;j++){
				var pCls=par[j].ParameterType;
				if(types[j]==par[j].ParameterType) continue;
				if(pCls==typeof(object)) continue;

				var type=types[j];


				if(pCls.IsPrimitive){
					if(!CanChangePrimitive(type.UnderlyingSystemType,pCls.UnderlyingSystemType,null)) break;
				} else{
					if(!pCls.IsAssignableFrom(type)) break;
				}
			}
			if(j==types.Length) candidates[curIdx++]=candidates[i];
		}
		if(curIdx==0) return null;
		if(curIdx==1) return candidates[0];

		// Walk all of the methods looking the most specific method to invoke
		var currentMin=0;
		var ambiguous=false;
		var paramOrder=new int[types.Length];
		for(var i=0;i<types.Length;i++) paramOrder[i]=i;
		for(var i=1;i<curIdx;i++){
			var newMin=FindMostSpecificMethod(candidates[currentMin],paramOrder,null,candidates[i],paramOrder,null,types,null);
			if(newMin==0) ambiguous=true;
			else{
				if(newMin==2){
					ambiguous=false;
					currentMin=i;
				}
			}
		}
		if(ambiguous) throw new AmbiguousMatchException();
		return candidates[currentMin];
	}

	// Given a set of properties that match the base criteria, select one.
	public sealed override PropertyInfo? SelectProperty(BindingFlags bindingAttr,PropertyInfo[] match,Type? returnType,
	                                                    Type[]? indexes,ParameterModifier[]? modifiers){
		// Allow a null indexes array. But if it is not null, every element must be non-null as well.
		if(indexes!=null){
			foreach(var index in indexes){
				if(index==null) throw new ArgumentNullException(nameof(indexes));
			}
		}

		if(match==null||match.Length==0) throw new ArgumentException(nameof(match));

		var candidates=(PropertyInfo[])match.Clone();

		int i,j=0;

		// Find all the properties that can be described by type indexes parameter
		var curIdx=0;
		var indexesLength=indexes?.Length??0;
		for(i=0;i<candidates.Length;i++){
			if(indexes!=null){
				var par=candidates[i].GetIndexParameters();
				if(par.Length!=indexesLength) continue;

				for(j=0;j<indexesLength;j++){
					var pCls=par[j].ParameterType;

					// If the classes  exactly match continue
					if(pCls==indexes[j]) continue;
					if(pCls==typeof(object)) continue;

					if(pCls.IsPrimitive){
						if(!CanChangePrimitive(indexes[j].UnderlyingSystemType,pCls.UnderlyingSystemType,null)) break;
					} else{
						if(!pCls.IsAssignableFrom(indexes[j])) break;
					}
				}
			}

			if(j==indexesLength){
				if(returnType!=null){
					if(candidates[i].PropertyType.IsPrimitive){
						if(!CanChangePrimitive(returnType.UnderlyingSystemType,candidates[i].PropertyType.UnderlyingSystemType,null)) continue;
					} else{
						if(!candidates[i].PropertyType.IsAssignableFrom(returnType)) continue;
					}
				}
				candidates[curIdx++]=candidates[i];
			}
		}
		if(curIdx==0) return null;
		if(curIdx==1) return candidates[0];

		// Walk all of the properties looking the most specific method to invoke
		var currentMin=0;
		var ambiguous=false;
		var paramOrder=new int[indexesLength];
		for(i=0;i<indexesLength;i++) paramOrder[i]=i;
		for(i=1;i<curIdx;i++){
			var newMin=FindMostSpecificType(candidates[currentMin].PropertyType,candidates[i].PropertyType,returnType);
			if(newMin==0&&indexes!=null)
				newMin=FindMostSpecific(candidates[currentMin].GetIndexParameters(),
				                        paramOrder,
				                        null,
				                        candidates[i].GetIndexParameters(),
				                        paramOrder,
				                        null,
				                        indexes,
				                        null);
			if(newMin==0){
				newMin=FindMostSpecificProperty(candidates[currentMin],candidates[i]);
				if(newMin==0) ambiguous=true;
			}
			if(newMin==2){
				ambiguous=false;
				currentMin=i;
			}
		}

		if(ambiguous) throw new AmbiguousMatchException();
		return candidates[currentMin];
	}

	// ChangeType
	// The default binder doesn't support any change type functionality.
	// This is because the default is built into the low level invoke code.
	public override object ChangeType(object value,Type type,CultureInfo? cultureInfo){
		return StaticallyTypedUtils.Cast(value,type);
	}

	public sealed override void ReorderArgumentArray(ref object?[] args,object state){
		var binderState=(BinderState)state;
		ReorderParams(binderState.ArgsMap,args);
		if(binderState.IsParamArray){
			var paramArrayPos=args.Length-1;
			if(args.Length==binderState.OriginalSize){
				args[paramArrayPos]=((object[])args[paramArrayPos]!)[0];
			} else{
				// must be args.Length < state.originalSize
				var newArgs=new object[args.Length];
				Array.Copy(args,newArgs,paramArrayPos);
				for(int i=paramArrayPos,j=0;i<newArgs.Length;i++,j++){
					newArgs[i]=((object[])args[paramArrayPos]!)[j];
				}
				args=newArgs;
			}
		} else{
			if(args.Length>binderState.OriginalSize){
				var newArgs=new object[binderState.OriginalSize];
				Array.Copy(args,newArgs,binderState.OriginalSize);
				args=newArgs;
			}
		}
	}

	private static int FindMostSpecific(ParameterInfo[] p1,int[] paramOrder1,Type? paramArrayType1,
	                                    ParameterInfo[] p2,int[] paramOrder2,Type? paramArrayType2,
	                                    Type?[] types,object?[]? args){
		// A method using params is always less specific than one not using params
		if(paramArrayType1!=null&&paramArrayType2==null) return 2;
		if(paramArrayType2!=null&&paramArrayType1==null) return 1;

		// now either p1 and p2 both use params or neither does.

		var p1Less=false;
		var p2Less=false;

		for(var i=0;i<types.Length;i++){
			if(args!=null&&args[i]==Type.Missing) continue;

			Type c1,c2;

			// If a param array is present, then either
			//      the user re-ordered the parameters in which case
			//          the argument to the param array is either an array
			//              in which case the params is conceptually ignored and so paramArrayType1 == null
			//          or the argument to the param array is a single element
			//              in which case paramOrder[i] == p1.Length - 1 for that element
			//      or the user did not re-order the parameters in which case
			//          the paramOrder array could contain indexes larger than p.Length - 1 (see VSW 577286)
			//          so any index >= p.Length - 1 is being put in the param array

			if(paramArrayType1!=null&&paramOrder1[i]>=p1.Length-1) c1=paramArrayType1;
			else c1=p1[paramOrder1[i]].ParameterType;

			if(paramArrayType2!=null&&paramOrder2[i]>=p2.Length-1) c2=paramArrayType2;
			else c2=p2[paramOrder2[i]].ParameterType;

			if(c1==c2) continue;

			switch(FindMostSpecificType(c1,c2,types[i])){
				case 0:return 0;
				case 1:
					p1Less=true;
					break;
				case 2:
					p2Less=true;
					break;
			}
		}

		// Two way p1Less and p2Less can be equal.  All the arguments are the
		//  same they both equal false, otherwise there were things that both
		//  were the most specific type on....
		if(p1Less==p2Less){
			// if we cannot tell which is a better match based on parameter types (p1Less == p2Less),
			// let's see which one has the most matches without using the params array (the longer one wins).
			if(!p1Less&&args!=null){
				if(p1.Length>p2.Length){
					return 1;
				} else if(p2.Length>p1.Length){
					return 2;
				}
			}

			return 0;
		} else{
			return p1Less?1:2;
		}
	}

	private static int FindMostSpecificType(Type c1,Type c2,Type? t){
		// If the two types are exact move on...
		if(c1==c2) return 0;


		if(c1==t) return 1;

		if(c2==t) return 2;

		bool c1FromC2;
		bool c2FromC1;

		if(c1.IsByRef||c2.IsByRef){
			if(c1.IsByRef&&c2.IsByRef){
				c1=c1.GetElementType()!;
				c2=c2.GetElementType()!;
			} else if(c1.IsByRef){
				if(c1.GetElementType()==c2) return 2;

				c1=c1.GetElementType()!;
			} else// if (c2.IsByRef)
			{
				if(c2.GetElementType()==c1) return 1;

				c2=c2.GetElementType()!;
			}
		}

		if(c1.IsPrimitive&&c2.IsPrimitive){
			c1FromC2=CanChangePrimitive(c2,c1,null);
			c2FromC1=CanChangePrimitive(c1,c2,null);
		} else{
			c1FromC2=c1.IsAssignableFrom(c2);
			c2FromC1=c2.IsAssignableFrom(c1);
		}

		if(c1FromC2==c2FromC1) return 0;

		if(c1FromC2){
			return 2;
		} else{
			return 1;
		}
	}

	private static int FindMostSpecificMethod(MethodBase m1,int[] paramOrder1,Type? paramArrayType1,
	                                          MethodBase m2,int[] paramOrder2,Type? paramArrayType2,
	                                          Type?[] types,object?[]? args){
		// Find the most specific method based on the parameters.
		var res=FindMostSpecific(m1.GetParameters(),paramOrder1,paramArrayType1,
		                         m2.GetParameters(),paramOrder2,paramArrayType2,types,args);

		// If the match was not ambiguous then return the result.
		if(res!=0) return res;

		// Check to see if the methods have the exact same name and signature.
		if(CompareMethodSig(m1,m2)){
			// Determine the depth of the declaring types for both methods.
			var hierarchyDepth1=GetHierarchyDepth(m1.DeclaringType!);
			var hierarchyDepth2=GetHierarchyDepth(m2.DeclaringType!);

			// The most derived method is the most specific one.
			if(hierarchyDepth1==hierarchyDepth2){
				return 0;
			} else if(hierarchyDepth1<hierarchyDepth2){
				return 2;
			} else{
				return 1;
			}
		}

		// The match is ambiguous.
		return 0;
	}

	private static int FindMostSpecificField(FieldInfo cur1,FieldInfo cur2){
		// Check to see if the fields have the same name.
		if(cur1.Name==cur2.Name){
			var hierarchyDepth1=GetHierarchyDepth(cur1.DeclaringType!);
			var hierarchyDepth2=GetHierarchyDepth(cur2.DeclaringType!);

			if(hierarchyDepth1==hierarchyDepth2){
				Debug.Assert(cur1.IsStatic!=cur2.IsStatic,"hierarchyDepth1 == hierarchyDepth2");
				return 0;
			} else if(hierarchyDepth1<hierarchyDepth2) return 2;
			else return 1;
		}

		// The match is ambiguous.
		return 0;
	}

	private static int FindMostSpecificProperty(PropertyInfo cur1,PropertyInfo cur2){
		// Check to see if the fields have the same name.
		if(cur1.Name==cur2.Name){
			var hierarchyDepth1=GetHierarchyDepth(cur1.DeclaringType!);
			var hierarchyDepth2=GetHierarchyDepth(cur2.DeclaringType!);

			if(hierarchyDepth1==hierarchyDepth2){
				return 0;
			} else if(hierarchyDepth1<hierarchyDepth2) return 2;
			else return 1;
		}

		// The match is ambiguous.
		return 0;
	}

	private static bool CompareMethodSig(MethodBase m1,MethodBase m2){
		var params1=m1.GetParameters();
		var params2=m2.GetParameters();

		if(params1.Length!=params2.Length) return false;

		var numParams=params1.Length;
		for(var i=0;i<numParams;i++){
			if(params1[i].ParameterType!=params2[i].ParameterType) return false;
		}

		return true;
	}

	private static int GetHierarchyDepth(Type t){
		var depth=0;

		var currentType=t;
		do{
			depth++;
			currentType=currentType.BaseType;
		} while(currentType!=null);

		return depth;
	}

	// This method will sort the vars array into the mapping order stored
	//  in the paramOrder array.
	private static void ReorderParams(int[] paramOrder,object?[] vars){
		object?[] varsCopy=new object[vars.Length];
		for(var i=0;i<vars.Length;i++) varsCopy[i]=vars[i];

		for(var i=0;i<vars.Length;i++) vars[i]=varsCopy[paramOrder[i]];
	}

	// This method will create the mapping between the Parameters and the underlying
	//  data based upon the names array.  The names array is stored in the same order
	//  as the values and maps to the parameters of the method.  We store the mapping
	//  from the parameters to the names in the paramOrder array.  All parameters that
	//  don't have matching names are then stored in the array in order.
	private static bool CreateParamOrder(int[] paramOrder,ParameterInfo[] pars,string[] names){
		var used=new bool[pars.Length];

		// Mark which parameters have not been found in the names list
		for(var i=0;i<pars.Length;i++) paramOrder[i]=-1;
		// Find the parameters with names.
		for(var i=0;i<names.Length;i++){
			int j;
			for(j=0;j<pars.Length;j++){
				if(names[i].Equals(pars[j].Name)){
					paramOrder[j]=i;
					used[i]=true;
					break;
				}
			}
			// This is an error condition.  The name was not found.  This
			//  method must not match what we sent.
			if(j==pars.Length) return false;
		}

		// Now we fill in the holes with the parameters that are unused.
		var pos=0;
		for(var i=0;i<pars.Length;i++){
			if(paramOrder[i]==-1){
				for(;pos<pars.Length;pos++){
					if(!used[pos]){
						paramOrder[i]=pos;
						pos++;
						break;
					}
				}
			}
		}
		return true;
	}

	// CanChangePrimitive
	// This will determine if the source can be converted to the target type
	public static bool CanChangePrimitive(Type source,Type target,object? sourceValue){
		if((source==typeof(IntPtr)&&target==typeof(IntPtr))||
		   (source==typeof(UIntPtr)&&target==typeof(UIntPtr))) return true;

		var widerCodes=PrimitiveConversions[(int)(Type.GetTypeCode(source))];
		var targetCode=(Primitives)(1<<(int)(Type.GetTypeCode(target)));

		return (widerCodes&targetCode)!=0;
	}

	private static readonly Primitives[] PrimitiveConversions={
		/* Empty    */ 0,// not primitive
		/* Object   */ 0,// not primitive
		/* DBNull   */ 0,// not primitive
		/* Boolean  */ Primitives.Boolean,
		/* Char     */ Primitives.Char|Primitives.UInt16|Primitives.UInt32|Primitives.Int32|Primitives.UInt64|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* SByte    */ Primitives.SByte|Primitives.Int16|Primitives.Int32|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* Byte     */ Primitives.Byte|Primitives.Char|Primitives.UInt16|Primitives.Int16|Primitives.UInt32|Primitives.Int32|Primitives.UInt64|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* Int16    */ Primitives.Int16|Primitives.Int32|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* UInt16   */ Primitives.UInt16|Primitives.UInt32|Primitives.Int32|Primitives.UInt64|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* Int32    */ Primitives.Int32|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* UInt32   */ Primitives.UInt32|Primitives.UInt64|Primitives.Int64|Primitives.Single|Primitives.Double,
		/* Int64    */ Primitives.Int64|Primitives.Single|Primitives.Double,
		/* UInt64   */ Primitives.UInt64|Primitives.Single|Primitives.Double,
		/* Single   */ Primitives.Single|Primitives.Double,
		/* Double   */ Primitives.Double,
		/* Decimal  */ Primitives.Decimal,
		/* DateTime */ Primitives.DateTime,
		/* [Unused] */ 0,
		/* String   */ Primitives.String,
	};

	[Flags]
	private enum Primitives{
		Boolean=1<<TypeCode.Boolean,
		Char=1<<TypeCode.Char,
		SByte=1<<TypeCode.SByte,
		Byte=1<<TypeCode.Byte,
		Int16=1<<TypeCode.Int16,
		UInt16=1<<TypeCode.UInt16,
		Int32=1<<TypeCode.Int32,
		UInt32=1<<TypeCode.UInt32,
		Int64=1<<TypeCode.Int64,
		UInt64=1<<TypeCode.UInt64,
		Single=1<<TypeCode.Single,
		Double=1<<TypeCode.Double,
		Decimal=1<<TypeCode.Decimal,
		DateTime=1<<TypeCode.DateTime,
		String=1<<TypeCode.String,
	}

	private class BinderState{
		internal readonly int[] ArgsMap;
		internal readonly int OriginalSize;
		internal readonly bool IsParamArray;

		internal BinderState(int[] argsMap,int originalSize,bool isParamArray){
			ArgsMap=argsMap;
			OriginalSize=originalSize;
			IsParamArray=isParamArray;
		}
	}
}