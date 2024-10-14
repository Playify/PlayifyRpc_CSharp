using System.Reflection;
using PlayifyRpc.Types.Data.Objects;

namespace PlayifyRpc.Internal.Data;

public static partial class DynamicCaster{
	private static IEnumerable<(string,object?)>? TryGetObjectProps(object? x)
		=>x switch{
			ObjectTemplateBase obj=>obj.GetProperties(),
			_=>null,
		};

	private static class DefaultCasters{

#if NETFRAMEWORK
		public static object? InstanceOfCheck(object? value,Type type,bool throwOnError)=>type.IsInstanceOfType(value)?value:ContinueWithNext;
#else
		public static object InstanceOfCheck(object? value,Type type,bool throwOnError)=>type.IsInstanceOfType(value)?value:ContinueWithNext;
#endif


		public static object? ImplicitConversion(object? value,Type type,bool throwOnError){
			IEnumerable<MethodInfo> methods=type.GetMethods(BindingFlags.Public|BindingFlags.Static);
			if(value!=null) methods=value.GetType().GetMethods(BindingFlags.Public|BindingFlags.Static).Concat(methods);
			foreach(var mi in methods){
				if(mi.Name!="op_Implicit") continue;
				if(!type.IsAssignableFrom(mi.ReturnType)) continue;
				if(mi.GetParameters().FirstOrDefault() is not{} param) continue;
				if(TryCast(value,param.ParameterType,out var par,false)){
					return mi.Invoke(null,[par]);
				}
			}
			return ContinueWithNext;
		}

		public static object? TryParse(object? value,Type type,bool throwOnError){
			if(type.GetMethod("TryParse",
				   BindingFlags.Public|BindingFlags.Static|BindingFlags.InvokeMethod,
				   null,
				   [typeof(string),type.MakeByRefType()],
				   null) is not{} tryParse) return ContinueWithNext;
			if(!TryCast(value,out string s)) return ContinueWithNext;

			var parameters=new[]{s,type.IsValueType?Activator.CreateInstance(type):null};
			if(tryParse.Invoke(null,parameters) is true)
				return parameters[1];
			return ContinueWithNext;
		}
	}
}