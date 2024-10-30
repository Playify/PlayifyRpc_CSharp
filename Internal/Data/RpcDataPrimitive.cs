using System.Globalization;
using System.Numerics;
using JetBrains.Annotations;
using PlayifyUtility.Jsons;
using PlayifyUtility.Utils;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

[PublicAPI]
public readonly partial struct RpcDataPrimitive:IEquatable<RpcDataPrimitive>{
	public static readonly object ContinueWithNext=new();

	#region Data
	private readonly object? _data;
	private readonly List<object?>? _already;
	public override bool Equals(object? obj)=>obj is RpcDataPrimitive other&&this==other;
	public override int GetHashCode()=>_data?.GetHashCode()??0;
	public static bool operator !=(RpcDataPrimitive left,RpcDataPrimitive right)=>!(left==right);

	public static bool operator ==(RpcDataPrimitive left,RpcDataPrimitive right)=>Equals(left._data,right._data);

	public bool Equals(RpcDataPrimitive other)=>this==other;
	#endregion

	#region Parse & ToString
	public override string ToString()=>ToString(true);

	public string ToString(bool pretty)=>ToString(pretty,null);

	private string ToString(bool pretty,Stack<RpcDataPrimitive>? already){
		if(IsNull()) return "null";
		if(IsBool(out var b)) return b?"true":"false";
		if(IsBigIntegerAndNothingElse(out var big)) return big.ToString();
		if(IsNumber(long.MinValue,long.MaxValue,out var l)) return l.ToString();
		if(IsNumber(out var d)) return d.ToString(CultureInfo.InvariantCulture);
		if(IsString(out var s)) return JsonString.Escape(s);
		if(already?.Contains(this)??false) return "<<Cyclic Reference>>";
		if(IsArray(out var childs,out var len)){
			if(len==0) return "[]";
			(already??=[]).Push(this);
			s=pretty
				  ?$"[\n\t{childs
				           .Select(c=>c.ToString(true,already))
				           .Join(",\n")
				           .Replace("\n","\n\t")}\n]"
				  :$"[{childs
				       .Select(c=>c.ToString(false,already))
				       .Join(",")}]";
			already.Pop();
			return s;
		}
		if(IsObject(out var tuples)){
			(already??=[]).Push(this);
			if(!pretty) s="{"+tuples.Select(kv=>$"{JsonString.Escape(kv.key)}:{kv.value.ToString(false,already)}").Join(",")+"}";
			else if((s=tuples.Select(kv=>$"{JsonString.Escape(kv.key)}:{kv.value.ToString(true,already)}").Join(",\n"))=="") s="{}";
			else s="{\n\t"+s.Replace("\n","\n\t")+"\n}";
			already.Pop();
			return s;
		}
		if(IsCustom(out object custom)) return $"{custom}";

		return $"<<Invalid: {_data} of type {RpcTypeStringifier.FromType(_data?.GetType()??typeof(object))}>>";
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
		if(BigInteger.TryParse(s,out var big)) return new RpcDataPrimitive(big);
		if(s[0]=='"'&&JsonString.TryUnescape(s,out var asString)) return new RpcDataPrimitive(asString);
		if(Json.ParseOrNull(s) is{} json) return From(json);
		return null;
	}
	#endregion

	#region Null
	public RpcDataPrimitive()=>_data=null;
	public bool IsNull()=>_data==null;
	#endregion

	#region Boolean
	public RpcDataPrimitive(bool @bool)=>_data=@bool;
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
	public RpcDataPrimitive(long number)=>_data=number;
	public RpcDataPrimitive(double number)=>_data=number;
	public RpcDataPrimitive(BigInteger number)=>_data=number;
	public bool IsNumber(long min,long max,out long l){
		switch(_data){
			case long ll:
				l=ll;
				return l>=min&&l<=max;
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			case double d when d>=min&&d<=max&&Math.Floor(d)==d:
				l=(long)d;
				return true;
			case BigInteger big when big>=min&&big<=max:
				l=(long)big;
				return true;
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
			case double dd:
				d=dd;
				return true;
			case BigInteger big:
				d=(double)big;
				return true;
			default:
				d=default;
				return false;
		}
	}

	public bool IsBigIntegerAndNothingElse(out BigInteger big){
		if(_data is BigInteger bb){
			big=bb;
			return true;
		}
		big=default;
		return false;
	}
	#endregion

	#region String
	public RpcDataPrimitive(string @string)=>_data=@string;
	public RpcDataPrimitive(char @char)=>_data=@char;
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