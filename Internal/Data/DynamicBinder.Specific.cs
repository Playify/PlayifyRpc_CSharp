using System.Reflection;

namespace PlayifyRpc.Internal.Data;

public partial class DynamicBinder{
	private static int FindMostSpecificMethod((MethodBase method,ParameterInfo[] par) tuple1,Type? paramArrayType1,
		(MethodBase method,ParameterInfo[] par) tuple2,Type? paramArrayType2,
		Type?[] argTypes,object?[] args){
		// A method using params is always less specific than one not using params
		if(paramArrayType1!=null&&paramArrayType2==null) return 2;
		if(paramArrayType2!=null&&paramArrayType1==null) return 1;

		// now either p1 and p2 both use params or neither does.
		var p1Less=false;
		var p2Less=false;
		for(var i=0;i<argTypes.Length;i++){
			if(args!=null&&args[i]==Type.Missing) continue;

			var c1=paramArrayType1!=null&&i>=tuple1.par.Length-1?paramArrayType1:tuple1.par[i].ParameterType;
			var c2=paramArrayType2!=null&&i>=tuple2.par.Length-1?paramArrayType2:tuple2.par[i].ParameterType;

			if(c1==c2) continue;

			switch(FindMostSpecificType(c1,c2,argTypes[i])){
				case 0:
					p1Less=p2Less=true;
					break;
				case 1:
					p1Less=true;
					break;
				case 2:
					p2Less=true;
					break;
			}
			if(p1Less&&p2Less) break;
		}

		// Two ways p1Less and p2Less can be equal. All the arguments are the
		//  same they both equal false, otherwise there were things that both
		//  were the most specific type on....
		if(p1Less!=p2Less) return p1Less?1:2;
		// if we cannot tell which is a better match based on parameter types (p1Less == p2Less),
		// let's see which one has the most matches without using the params array (the longer one wins).
		if(!p1Less&&args!=null){
			if(tuple1.par.Length>tuple2.par.Length) return 1;
			if(tuple2.par.Length>tuple1.par.Length) return 2;
		}


		// Check to see if the methods have the exact same name and signature.
		var params1=tuple1.par;
		var params2=tuple2.par;

		if(params1.Length!=params2.Length) return 0;

		var numParams=params1.Length;
		for(var i=0;i<numParams;i++)
			if(params1[i].ParameterType!=params2[i].ParameterType)
				return 0;


		// Determine the depth of the declaring types for both methods.
		var hierarchyDepth1=GetHierarchyDepth(tuple1.method.DeclaringType!);
		var hierarchyDepth2=GetHierarchyDepth(tuple2.method.DeclaringType!);

		// The most derived method is the most specific one.
		if(hierarchyDepth1==hierarchyDepth2) return 0;
		if(hierarchyDepth1<hierarchyDepth2) return 2;
		return 1;
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
			c1FromC2=CanChangePrimitive(c2,c1);
			c2FromC1=CanChangePrimitive(c1,c2);
		} else{
			c1FromC2=c1.IsAssignableFrom(c2);
			c2FromC1=c2.IsAssignableFrom(c1);
		}

		if(c1FromC2==c2FromC1) return 0;
		return c1FromC2?2:1;
	}

	private static int GetHierarchyDepth(Type? t){
		var depth=0;
		for(;t!=null;t=t.BaseType)
			depth++;
		return depth;
	}
}