using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using PlayifyUtility.Utils.Extensions;

namespace PlayifyRpc.Internal.Data;

public readonly partial struct RpcDataPrimitive{

	public static RpcDataPrimitive? Parse(string s)=>Parser.ParseFromString(s);

	private static class Parser{
		public static RpcDataPrimitive? ParseFromString(string s){
			var reader=new StringReader(s);
			if(!Parse(reader).TryGet(out var found)) return null;
			if(NextPeek(reader)!=-1) return null;
			return found;
		}

		private static RpcDataPrimitive? Parse(TextReader r)
			=>NextPeek(r) switch{
				'{'=>ParseObject(r),
				'['=>ParseArray(r),
				'"'=>ParseString(r,'"') is{} s?new RpcDataPrimitive(s):null,
				'\''=>ParseString(r,'\'') is{} s?new RpcDataPrimitive(s):null,
				'n'=>ParseLiteral(r,"null")?new RpcDataPrimitive():null,
				't'=>ParseLiteral(r,"true")?new RpcDataPrimitive(true):null,
				'f'=>ParseLiteral(r,"false")?new RpcDataPrimitive(false):null,
				'N'=>ParseLiteral(r,"NaN")?new RpcDataPrimitive(double.NaN):null,
				'I'=>ParseLiteral(r,"Infinity")?new RpcDataPrimitive(double.PositiveInfinity):null,
				(>='0' and <='9') or '+' or '-' or '.'=>ParseNumber(r),
				'/'=>ParseRegex(r),
				_=>null,
			};

		private static bool ParseLiteral(TextReader r,string s)=>s.All(c=>r.Read()==c);

		private static RpcDataPrimitive? ParseArray(TextReader r){
			if(r.Read()!='[') return null;
			var o=new List<RpcDataPrimitive>();

			var c=NextPeek(r);
			switch(c){
				case ',':
					r.Read();
					return NextRead(r)==']'?new RpcDataPrimitive(o):null;
				case ']':{
					r.Read();
					return new RpcDataPrimitive(o);
				}
			}
			while(true){
				if(!Parse(r).TryGet(out var child)) return null;
				o.Add(child);
				c=NextRead(r);
				if(c==']') return new RpcDataPrimitive(o);
				if(c!=',') return null;
				c=NextPeek(r);
				if(c!=']') continue;
				r.Read();
				return new RpcDataPrimitive(o);
			}
		}

		private static RpcDataPrimitive? ParseObject(TextReader r){
			if(r.Read()!='{') return null;
			var o=new List<(string key,RpcDataPrimitive value)>();
			var c=NextPeek(r);
			switch(c){
				case ',':
					r.Read();
					return NextRead(r)=='}'?new RpcDataPrimitive(()=>o):null;
				case '}':{
					r.Read();
					return new RpcDataPrimitive(()=>o);
				}
			}
			while(true){
				if(ParseString(r) is not{} key) return null;
				if(NextRead(r)!=':') return null;
				if(!Parse(r).TryGet(out var child)) return null;
				o.Add((key,child));
				c=NextRead(r);
				if(c=='}') return new RpcDataPrimitive(()=>o);
				if(c!=',') return null;
				c=NextPeek(r);
				if(c!='}') continue;
				r.Read();
				return new RpcDataPrimitive(()=>o);
			}
		}


		private static RpcDataPrimitive? ParseNumber(TextReader r){
			var c=r.Read();
			if(c is not ((>='0' and <='9') or '+' or '-' or '.')) return null;

			var builder=new StringBuilder();
			builder.Append((char)c);

			var allowDot=c!='.';

			if(r.Peek()=='I')
				return builder[0] is '+' or '-'&&"Infinity".All(n=>r.Read()==n)
					       ?new RpcDataPrimitive(builder[0]=='+'
						                             ?double.PositiveInfinity
						                             :double.NegativeInfinity)
					       :null;

			var allowE=true;
			var allowSign=false;
			while(true){
				c=r.Peek();
				switch(c){
					case >='0' and <='9':
						builder.Append((char)c);
						break;
					case '.' when allowDot:
						builder.Append('.');
						allowDot=false;
						break;
					case 'e' or 'E' when allowE&&(builder.Length>1||builder[0]!='-'):
						builder.Append((char)c);
						allowE=false;
						allowSign=true;
						allowDot=false;

						r.Read();//remove peeked value from stream
						continue;
					case '+' or '-' when allowSign:
						builder.Append((char)c);
						break;

					case 'n':
						r.Read();//remove peeked value from stream
						if(!BigInteger.TryParse(builder.ToString(),NumberStyles.Any,CultureInfo.InvariantCulture,out var big)) return null;
						return new RpcDataPrimitive(big);
					default:
						if(!double.TryParse(builder.ToString(),NumberStyles.Any,CultureInfo.InvariantCulture,out var dbl)) return null;
						return new RpcDataPrimitive(dbl);
				}
				r.Read();//remove peeked value from stream
				allowSign=false;
			}
		}

		private static readonly Dictionary<int,RegexOptions> RegexOptionsMap=new(){
			{'i',RegexOptions.IgnoreCase},
			{'m',RegexOptions.Multiline},
		};

		private static RpcDataPrimitive? ParseRegex(TextReader r){
			var pattern=ParseString(r,'/');
			if(pattern==null) return null;

			RegexOptions options=default;

			while(true){
				var peek=r.Peek();
				if(
					!RegexOptionsMap.TryGetValue(peek,out var newFlag)
					||(options&newFlag)!=0//Already added that
				) return From(new Regex(pattern,options));
				options|=newFlag;
				r.Read();
			}
		}


		private static string? ParseString(TextReader r)=>r.Peek() switch{
			'"'=>ParseString(r,'"'),
			'\''=>ParseString(r,'\''),
			_=>null,
		};

		private static string? ParseString(TextReader r,char quoteType){
			if(r.Read()!=quoteType) return null;


			var str=new StringBuilder();
			var escape=false;
			while(true)
				if(escape){
					switch(r.Read()){
						case -1:return null;
						case var c when c==quoteType:
							str.Append(quoteType);
							break;
						case var c when quoteType=='/':
							str.Append('\\').Append(c);//Regex has no escape sequences except /
							break;
						case 'b':
							str.Append('\b');
							break;
						case 'f':
							str.Append('\f');
							break;
						case 'r':
							str.Append('\r');
							break;
						case 'n':
							str.Append('\n');
							break;
						case 't':
							str.Append('\t');
							break;
						case 'u':
							var cp=0;
							for(var i=0;i<4;i++){
								cp<<=4;
								var c=r.Read();
								if(!(c switch{
										    >='0' and <='9'=>c-'0',
										    >='a' and <='f'=>c-'a'+10,
										    >='A' and <='F'=>c-'A'+10,
										    //-1=>throw new EndOfStreamException(),
										    _=>(int?)null,
									    }).TryGet(out var hex)) return null;
								cp|=hex;
							}
							str.Append(cp is >=55296 and <=57343
								           ?char.ToString((char)cp)//Surrogate codepoint value
								           :char.ConvertFromUtf32(cp));
							break;
						case var c://Defaults to just using the char as it is
							str.Append(c);
							break;
					}
					escape=false;
				} else
					switch(r.Read()){
						case var c when c==quoteType:
							return str.ToString();
						case -1:return null;
						case '\\':
							escape=true;
							break;
						case var c:
							str.Append((char)c);
							break;
					}
		}


		#region Reading
		private static int NextRead(TextReader r){
			while(true){
				var c=r.Read();
				if(c=='/')
					if(!SkipComment(r)) return -1;//Error
					else continue;
				if(!IsWhitespace(c)) return c;
			}
		}

		private static int NextPeek(TextReader r){
			while(true){
				var c=r.Peek();
				if(c=='/'){
					r.Read();
					if(!SkipComment(r)) return -1;//Error
					continue;
				}
				if(!IsWhitespace(c)) return c;
				r.Read();
			}
		}

		private static bool SkipComment(TextReader r){
			var read=r.Read();
			switch(read){
				case '*':
					var c=r.Read();
					while(true)
						if(c==-1) return false;
						else if(c=='*'){
							c=r.Read();
							if(c=='/') return true;
						} else c=r.Read();
				case '/':
					while(true){
						switch(r.Read()){
							case '\r':
							case '\n':
								return true;
							case -1:
								return false;
						}
					}
				default:return false;
			}
		}

		private static bool IsWhitespace(int c)=>c is ' ' or '\r' or '\n' or '\t';
		#endregion

	}
}