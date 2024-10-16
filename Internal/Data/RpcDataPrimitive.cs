using System.Globalization;
using JetBrains.Annotations;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public readonly partial struct RpcDataPrimitive{
	public static readonly object ContinueWithNext=new();

	#region Data
	private readonly object? _data;
	private readonly List<object?>? _already;
	public override bool Equals(object? obj)=>obj is RpcDataPrimitive other&&this==other;
	public override int GetHashCode()=>_data?.GetHashCode()??0;
	public static bool operator !=(RpcDataPrimitive left,RpcDataPrimitive right)=>!(left==right);

	public static bool operator ==(RpcDataPrimitive left,RpcDataPrimitive right){
		if(Equals(left._data,right._data)) return true;
		if(left._data is char c1&&right._data is string{Length: 1} s1) return c1==s1[0];
		if(right._data is char c2&&left._data is string{Length: 1} s2) return c2==s2[0];

		return false;
	}
	#endregion

	#region Parse & ToString
	public override string ToString()=>ToString(true);

	public string ToString(bool pretty){
		if(IsNull()) return "null";
		if(IsBool(out var b)) return b?"true":"false";
		if(IsNumber(long.MinValue,long.MaxValue,out var l)) return l.ToString();
		if(IsNumber(ulong.MaxValue,out var ul)) return ul.ToString();
		if(IsNumber(out var d)) return d.ToString(CultureInfo.InvariantCulture);
		if(IsString(out var s)) return JsonString.Escape(s);
		if(IsArray(out var childs,out var len))
			if(len==0) return "[]";
			else if(pretty) return $"[\n\t{childs.Join("\n").Replace("\n","\t\n")}\n]";
			else return $"[{childs.Join(",")}]";
		if(IsObject(out var tuples))
			if(!pretty) return "{"+tuples.Select(kv=>$"{JsonString.Escape(kv.key)}:{kv.value.ToString(pretty)}").Join(",")+"}";
			else if((s=tuples.Select(kv=>$"{JsonString.Escape(kv.key)}:{kv.value.ToString(pretty)}").Join(",\n"))=="") return "{}";
			else return "{\n\t"+s.Replace("\n","\t\n")+"\n}";
		if(IsCustom(out object custom)) return custom.ToString();

		return $"<<Invalid: {_data} of type {RpcDataTypeStringifier.FromType(_data?.GetType()??typeof(object))}>>";
	}

	public static RpcDataPrimitive? Parse(string s){
		switch(s){
			case "":return null;
			case "null":return new RpcDataPrimitive();
			case "true":return new RpcDataPrimitive(true);
			case "false":return new RpcDataPrimitive(false);
		}
		if(int.TryParse(s,out var i)) return new RpcDataPrimitive(i);
		if(double.TryParse(s,out var d)) return new RpcDataPrimitive(d);
		if(s[0]=='"'&&JsonString.TryUnescape(s,out var asString)) return new RpcDataPrimitive(asString);
		if(Json.ParseOrNull(s) is{} json) return From(json);
		return null;
	}
	#endregion

	#region Null
	public RpcDataPrimitive(){
		_data=null;
	}

	public bool IsNull()=>_data==null;
	#endregion

	#region Boolean
	public RpcDataPrimitive(bool @bool){
		_data=@bool;
	}

	public bool IsBool(out bool b){
		if(_data is bool bb){
			b=bb;
			return true;
		}
		b=false;
		return false;
	}
	#endregion

	#region Number
	/*Use long and ulong instead
	public static RpcDataPrimitive Number(byte n)=>new(n);
	public static RpcDataPrimitive Number(sbyte n)=>new(n);
	public static RpcDataPrimitive Number(short n)=>new(n);
	public static RpcDataPrimitive Number(ushort n)=>new(n);
	public static RpcDataPrimitive Number(int n)=>new(n);
	public static RpcDataPrimitive Number(uint n)=>new(n);*/
	public RpcDataPrimitive(long number){
		_data=number;
	}

	public RpcDataPrimitive(ulong number){
		_data=number<=long.MaxValue?(long)number:number;
	}

	//public static RpcDataPrimitive Number(float n)=>new(n);//use double instead
	public RpcDataPrimitive(double number){
		_data=number;
	}

	//public static RpcDataPrimitive Number(decimal n)=>new(n);//not supported


	public bool IsNumber(long min,long max,out long l){
		switch(_data){
			case long ll:
				l=ll;
				return l>=min&&l<=max;
			case ulong ul and <=long.MaxValue:
				l=(long)ul;
				return l>=min&&l<=max;
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			case double d and >=long.MinValue and <=long.MaxValue when d==Math.Floor(d):
				l=(long)d;
				return l>=min&&l<=max;
			default:
				l=default;
				return false;
		}
	}

	public bool IsNumber(ulong max,out ulong l){
		switch(_data){
			case long ll and >=0:
				l=(ulong)ll;
				return l<=max;
			case ulong ul:
				l=ul;
				return l<=max;
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			case double d and >=0 and <=ulong.MaxValue when d==Math.Floor(d):
				l=(ulong)d;
				return l<=max;
			default:
				l=default;
				return false;
		}
	}

	public bool IsNumber(out double d){
		switch(_data){
			case long ll:
				d=ll;
				return true;
			case ulong ul:
				d=ul;
				return true;
			case double dd:
				d=dd;
				return true;
			default:
				d=default;
				return false;
		}
	}
	#endregion

	#region String
	public RpcDataPrimitive(string @string){
		_data=@string;
	}

	public RpcDataPrimitive(char @char){
		_data=@char;
	}


	public bool IsString(out string s){
		switch(_data){
			case string ss:
				s=ss;
				return true;
			case char c:
				s=char.ToString(c);
				return true;
			default:
				s=null!;
				return false;
		}
	}

	internal bool IsChar(out char c){
		switch(_data){
			case char cc:
				c=cc;
				return true;
			case string{Length: 1} s:
				c=s[0];
				return true;
			default:
				c=default;
				return false;
		}
	}
	#endregion

	#region Already
	public bool IsAlready<T>(out T value){
		if(_already!=null)
			foreach(var o in _already)
				if(o is T already){
					value=already;
					return true;
				}
		value=default!;
		return false;
	}

	public bool IsAlready(Type type,out object value){
		if(_already!=null)
			foreach(var o in _already)
				if(type.IsInstanceOfType(o)){
					value=o!;
					return true;
				}
		value=default!;
		return false;
	}

	public T AddAlready<T>(T value){
		_already?.Add(value);
		return value;
	}

	public object RemoveAlready(object? value){
		_already?.Remove(value);
		return ContinueWithNext;
	}

	public T? RemoveAlready<T>(object? value){
		_already?.Remove(value);
		return default;
	}
	#endregion

	#region Array
	public RpcDataPrimitive(Func<(IEnumerable<RpcDataPrimitive> elements,int count)> array){
		_data=array;
		_already=[];
	}

	public bool IsArray(out IEnumerable<RpcDataPrimitive> arr)=>IsArray(out arr,out _);

	public bool IsArray(out IEnumerable<RpcDataPrimitive> arr,out int length){
		if(_data is Func<(IEnumerable<RpcDataPrimitive> elements,int count)> func){
			(arr,length)=func();
			return true;
		}
		arr=null!;
		length=default!;
		return false;
	}
	#endregion

	#region Object
	public RpcDataPrimitive(Func<IEnumerable<(string key,RpcDataPrimitive value)>> @object){
		_data=@object;
		_already=[];
	}


	public bool IsObject(out IEnumerable<(string key,RpcDataPrimitive value)> entries){
		if(_data is Func<IEnumerable<(string key,RpcDataPrimitive value)>> func){
			entries=func();
			return true;
		}
		entries=null!;
		return false;
	}
	#endregion

	#region Custom
	public RpcDataPrimitive(object custom,WriteFunc write,Action? dispose){
		_data=(custom,write,dispose);
	}

	public bool IsCustom<T>(out T value)=>IsCustom(out value,out _);

	public bool IsCustom<T>(out T value,out WriteFunc write){
		if(_data is ValueTuple<object,WriteFunc,Action?>{Item1: T found} tuple){
			value=found;
			write=tuple.Item2;
			return true;
		}
		value=default!;
		write=default!;
		return false;
	}

	public bool IsDisposable(out Action a)
		=>_data is ValueTuple<object,WriteFunc,Action?> tuple
			  ?tuple.Item3.NotNull(out a!)
			  :FunctionUtils.TryGetNever(out a!);
	#endregion

}