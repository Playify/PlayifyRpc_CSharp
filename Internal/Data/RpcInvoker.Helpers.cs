using System.Reflection;

namespace PlayifyRpc.Internal.Data;

public static partial class RpcInvoker{
	private static bool FilterByArgLength(MethodInfo method,ParameterInfo[] parameters,RpcDataPrimitive[] args,out Type? paramArrayType){
		paramArrayType=null;
		if(parameters.Length==0) return args.Length==0||(method.CallingConvention&CallingConventions.VarArgs)!=0;

		if(args.Length<parameters.Length){//Not enough args

			// If the number of parameters is greater than the number of args then
			// we are in the situation were we may be using default values.
			var j=args.Length;
			for(;j<parameters.Length-1;j++)
				if(!parameters[j].HasDefaultValue)
					return false;

			var last=parameters[j];
			if(last.HasDefaultValue) return true;
			if(!last.ParameterType.IsArray||
			   !last.IsDefined(typeof(ParamArrayAttribute),true)) return false;

			paramArrayType=last.ParameterType.GetElementType();
			return true;
		}

		if(args.Length>parameters.Length){//Too many args
			//Check params array
			var last=parameters[parameters.Length-1];
			if(!last.ParameterType.IsArray||
			   !last.IsDefined(typeof(ParamArrayAttribute),true)) return false;

			paramArrayType=last.ParameterType.GetElementType();
		} else{
			//Normally in C# you can pass in an array for ParamArray as well as single objects.
			//But over RPC this is not allowed, therfore we don't add any check if the last value could be an array
			var last=parameters[parameters.Length-1];
			if(last.ParameterType.IsArray&&
			   last.IsDefined(typeof(ParamArrayAttribute),true)){
				paramArrayType=last.ParameterType.GetElementType();
			}
		}
		return true;
	}

	/*
	Returns true if method2 is more specific
	Returns false if method1 is more specific
	Returns null if ambiguous
	 */
	private static bool? FindMostSpecificMethod(
		MethodInfo method1,ParameterInfo[] parameters1,Type? paramArray1,
		MethodInfo method2,ParameterInfo[] parameters2,Type? paramArray2,
		int argsLength
	){
		// A method using params is always less specific than one not using params
		if(paramArray1!=null^paramArray2!=null) return paramArray1!=null;


		var less1=false;
		var less2=false;
		for(var i=0;i<argsLength;i++){
			var c1=paramArray1!=null&&i>=parameters1.Length-1?paramArray1:parameters1[i].ParameterType;
			var c2=paramArray2!=null&&i>=parameters2.Length-1?paramArray2:parameters2[i].ParameterType;

			if(c1==c2) continue;

			switch(FindMostSpecificType(c1,c2)){
				case null:
					less1=less2=true;
					break;
				case false:
					less1=true;
					break;
				case true:
					less2=true;
					break;
			}
			if(less1&&less2) break;
		}

		// Two ways p1Less and p2Less can be equal. All the arguments are the
		//  same they both equal false, otherwise there were things that both
		//  were the most specific type on....
		if(less1==less2){
			// if we cannot tell which is a better match based on parameter types (p1Less == p2Less),
			// let's see which one has the most matches without using the params array (the longer one wins).
			if(!less1&&parameters1.Length!=parameters2.Length)
				return parameters1.Length<parameters2.Length;
		} else return less2;
		
		//Check if methods have exact same signature
		if(parameters1.Length!=parameters2.Length) return null;
		for(var i=0;i<parameters1.Length;i++)
			if(parameters1[i].ParameterType!=parameters2[i].ParameterType)
				return null;

		var depth1=method1.DeclaringType;
		var depth2=method2.DeclaringType;
		while(depth1!=null&&depth2!=null){
			depth1=depth1.BaseType;
			depth2=depth2.BaseType;
		}
		if(depth1==depth2) return null;//Same depth
		return depth2!=null;
	}

	private static bool? FindMostSpecificType(Type c1,Type c2){
		if(c1==c2) return null;
		
		bool c1FromC2;
		bool c2FromC1;
		

		if(c1.IsPrimitive&&c2.IsPrimitive){
			c1FromC2=CanChangePrimitive(c2,c1);
			c2FromC1=CanChangePrimitive(c1,c2);
		} else{
			c1FromC2=c1.IsAssignableFrom(c2);
			c2FromC1=c2.IsAssignableFrom(c1);
		}

		if(c1FromC2==c2FromC1) return null;
		return c1FromC2;
	}


	#region Primitives
	private static bool CanChangePrimitive(Type source,Type target){
		if(source==typeof(IntPtr)&&target==typeof(IntPtr)||
		   source==typeof(UIntPtr)&&target==typeof(UIntPtr)) return true;

		var widerCodes=PrimitiveConversions[(int)Type.GetTypeCode(source)];
		var targetCode=(Primitives)(1<<(int)Type.GetTypeCode(target));

		return (widerCodes&targetCode)!=0;
	}

	private static readonly Primitives[] PrimitiveConversions=[
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
	];

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
	#endregion
}