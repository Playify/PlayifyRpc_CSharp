using System.Dynamic;
using System.Reflection;
using System.Text.RegularExpressions;
using PlayifyRpc.Types;
using PlayifyRpc.Types.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;

namespace PlayifyRpc.Internal.Data;

public static partial class DynamicTypeStringifier{
	private static class DefaultStringifiers{
		public static string? Primitives(State state)
			=>Type.GetTypeCode(state.Type) switch{
				TypeCode.Empty=>null,
				TypeCode.Object=>null,
				TypeCode.DBNull=>"null",
				TypeCode.Boolean=>state.TypeScript?"boolean":"bool",
				TypeCode.Char=>state.TypeScript?"string":"char",
				TypeCode.SByte=>state.TypeScript?"number":"sbyte",
				TypeCode.Byte=>state.TypeScript?"number":"byte",
				TypeCode.Int16=>state.TypeScript?"number":"short",
				TypeCode.UInt16=>state.TypeScript?"number":"ushort",
				TypeCode.Int32=>state.TypeScript?"number":"int",
				TypeCode.UInt32=>state.TypeScript?"number":"uint",
				TypeCode.Int64=>state.TypeScript?"bigint":"long",
				TypeCode.UInt64=>state.TypeScript?"bigint":"long",
				TypeCode.Single=>state.TypeScript?"number":"float",
				TypeCode.Double=>state.TypeScript?"number":"double",
				TypeCode.Decimal=>state.TypeScript?"number":"double",
				TypeCode.DateTime=>state.TypeScript?"Date":"DateTime",
				TypeCode.String=>"string",
				_=>null,
			};

		public static string? Nullables(State state){
			if(Nullable.GetUnderlyingType(state.Type) is{} nullable) return state.SubType(nullable,state.NullabilityInfo?.GenericTypeArguments[0])+(state.TypeScript?"|null":"?");
			return null;
		}

		public static string? Enums(State state){
			if(state.Type.IsEnum) return state.TypeName+state.Generics();
			if(state.Type.IsGenericType&&state.Type.GetGenericTypeDefinition()==typeof(StringEnum<>))
				return state.TypeScript
					       ?$"(keyof typeof {state.Type.GetGenericArguments()[0].Name})"
					       :$"{typeof(StringEnum<>).Name}<{state.Type.GetGenericArguments()[0].Name}>";
			return null;
		}

		public static string? Jsons(State state){
			if(state.Type==typeof(Json)) return state.TypeScript?"any":"object";
			if(state.Type==typeof(JsonBool)) return state.TypeScript?"boolean":"bool";
			if(state.Type==typeof(JsonNumber)) return state.TypeScript?"number":"double";
			if(state.Type==typeof(JsonNull)) return "null";
			if(state.Type==typeof(JsonString)) return "string";
			if(state.Type==typeof(JsonArray)) return state.TypeScript?"any[]":"object[]";
			if(state.Type==typeof(JsonObject)) return state.TypeScript?"object":nameof(ExpandoObject);
			if(state.Type==typeof(byte[])) return state.TypeScript?"Uint8Array":"byte[]";
			if(state.Type==typeof(object)) return state.TypeScript?"any":"object";
			return null;
		}

		public static string? ArraysTuples(State state){
			//Array
			if(state.Type.IsArray) return state.SubType(state.Type.GetElementType()!,state.NullabilityInfo?.ElementType)+"[]";
			//Tuple
			if(state.Type.IsGenericType&&DynamicCaster.ValueTupleTypes.Contains(state.Type.GetGenericTypeDefinition())){
				var names=EnumerableUtils.RepeatSelect(state.Type.GetGenericArguments().Length,state.TupleName).ToArray();
				var genericTypes=state.GenericTypes();
				return state.Tuple(genericTypes.Zip(names,(t,n)=>state.Parameter(n,t)));
			}
			return null;
		}

		public static string? SupportedRpcTypes(State state){
			if(typeof(Exception).IsAssignableFrom(state.Type)) return state.TypeScript?"RpcError":nameof(RpcException);
			if(typeof(Regex).IsAssignableFrom(state.Type)) return state.TypeScript?"RegExp":nameof(Regex);
			if(typeof(RpcObject).IsAssignableFrom(state.Type)) return nameof(RpcObject);
			if(typeof(RpcFunction).IsAssignableFrom(state.Type)) return nameof(RpcFunction);
			if(state.Type==typeof(ExpandoObject)) return state.TypeScript?"object":nameof(ExpandoObject);
			if(state.Type.IsGenericType&&state.Type.GetGenericTypeDefinition()==typeof(StringEnum<>)) return state.TypeScript?"{[key:string]:"+state.GenericTypes().Single()+"}":state.TypeName+state.Generics();
			if(typeof(ObjectTemplateBase).IsAssignableFrom(state.Type)) return/*state.TypeScript?"object":*/state.TypeName+state.Generics();
			if(state.Type.GetCustomAttribute<CustomDynamicTypeAttribute>()!=null) return state.TypeName+state.Generics();
			return null;
		}
	}
}