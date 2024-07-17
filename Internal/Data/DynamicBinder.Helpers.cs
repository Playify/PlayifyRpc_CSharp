using System.Reflection;

namespace PlayifyRpc.Internal.Data;

public partial class DynamicBinder{
	private static (MethodBase method,ParameterInfo[] par)?[] CreateCandidates(MethodBase[] match){
		var candidates=new (MethodBase method,ParameterInfo[] par)?[match.Length];
		for(var i=0;i<match.Length;i++){
			if(match[i].DeclaringType==typeof(object)) continue;
			var par=match[i].GetParameters();

			candidates[i]=(match[i],par);
		}
		return candidates;
	}

	private static bool FilterByArgLength(MethodBase method,ParameterInfo[] par,object?[] args,Type?[] argTypes,out Type? paramArrayType){
		paramArrayType=null;
		if(par.Length==0)
			return args.Length==0||(method.CallingConvention&CallingConventions.VarArgs)!=0;
		if(par.Length>args.Length){//Not enough args

			//Everything needs to have a default value
			int j;
			for(j=args.Length;j<par.Length-1;j++)
				if(par[j].DefaultValue==DBNull.Value)
					break;

			if(j!=par.Length-1) return false;

			if(par[j].DefaultValue==DBNull.Value){
				if(!par[j].ParameterType.IsArray||
				   !par[j].IsDefined(typeof(ParamArrayAttribute),true)) return false;

				paramArrayType=par[j].ParameterType.GetElementType();
			}
			return true;
		}
		if(par.Length<args.Length){//Too many args

			//check var ParamsArray
			var lastArgPos=par.Length-1;
			if(!par[lastArgPos].ParameterType.IsArray||
			   !par[lastArgPos].IsDefined(typeof(ParamArrayAttribute),true)) return false;

			paramArrayType=par[lastArgPos].ParameterType.GetElementType();
			return true;
		}

		{
			var lastArgPos=par.Length-1;
			if(par[lastArgPos].ParameterType.IsArray
			   &&par[lastArgPos].IsDefined(typeof(ParamArrayAttribute),true)
			   &&!par[lastArgPos].ParameterType.IsAssignableFrom(argTypes[lastArgPos]))
				paramArrayType=par[lastArgPos].ParameterType.GetElementType();
			return true;
		}
	}

	private static Type? GetNthArgType(BindingFlags bindingAttr,ParameterInfo[] par,object?[] args,Type? paramArrayType,int index){
		if(paramArrayType!=null&&index>=par.Length-1)
			return paramArrayType;
		var type=par[index].ParameterType;
		if(type.IsByRef) type=type.GetElementType();
		if(type==typeof(object)) return null;//Will accept all

		if((bindingAttr&BindingFlags.OptionalParamBinding)!=0&&args[index]==Type.Missing) return null;

		return type;
	}

	private static void CorrectArgs(MethodBase method,ParameterInfo[] par,ref object?[] args,Type? paramArrayType){
		// If the parameters and the args are not the same length or there is a paramArray
		//  then we need to create an argument array.

		if(par.Length==args.Length){
			if(paramArrayType!=null){
				var objs=new object[par.Length];
				var lastPos=par.Length-1;
				Array.Copy(args,objs,lastPos);
				objs[lastPos]=Array.CreateInstance(paramArrayType,1);
				((Array)objs[lastPos]).SetValue(DynamicCaster.Cast(args[lastPos],paramArrayType),0);
				args=objs;
			}
		} else if(par.Length>args.Length){
			object?[] objs=new object[par.Length];

			int i;
			for(i=0;i<args.Length;i++) objs[i]=args[i];

			for(;i<par.Length-1;i++) objs[i]=par[i].DefaultValue;

			if(paramArrayType!=null) objs[i]=Array.CreateInstance(paramArrayType,0);// create an empty array for the

			else objs[i]=par[i].DefaultValue;

			args=objs;
		} else if((method.CallingConvention&CallingConventions.VarArgs)==0){
			var objs=new object[par.Length];
			var paramArrayPos=par.Length-1;
			Array.Copy(args,objs,paramArrayPos);
			objs[paramArrayPos]=Array.CreateInstance(paramArrayType??throw new NullReferenceException(),args.Length-paramArrayPos);
			var array=(Array)objs[paramArrayPos];
			for(var i=0;i<args.Length-paramArrayPos;i++)
				array.SetValue(DynamicCaster.Cast(args[paramArrayPos+i],paramArrayType),i);

			args=objs;
		}
	}


	#region Primitives
	// This will determine if the source can be converted to the target type
	private static bool CanChangePrimitive(Type source,Type target){
		if((source==typeof(IntPtr)&&target==typeof(IntPtr))||
		   (source==typeof(UIntPtr)&&target==typeof(UIntPtr))) return true;

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