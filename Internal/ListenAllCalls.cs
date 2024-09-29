using PlayifyRpc.Connections;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Functions;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal;

internal static class ListenAllCalls{
	private static readonly HashSet<FunctionCallContext> Listening=new();

	internal static async Task Listen(FunctionCallContext ctx){
		lock(Listening) Listening.Add(ctx);
		try{
			await ctx.Task;
		} finally{
			lock(Listening) Listening.Remove(ctx);
		}
	}

	internal static void Broadcast(ServerConnection respondTo,string? type,DataInputBuff data){
		lock(Listening){
			if(Listening.Count==0) return;
			var clone=data.Clone();

			var method=clone.ReadString();

			var (b,off,len)=clone.GetBufferOffsetAndLength();
			var argsBytes=new byte[len];
			Array.Copy(b,off,argsBytes,0,len);
			
			
			var msg=new StringMap<object?>{
				{"name",respondTo.Name},
				{"id",respondTo.Id},
				{"prettyName",respondTo.PrettyName},
				{"type",type},
				{"method",method},
				{"argsBytes",argsBytes},
			};

			try{
				var already=new Dictionary<int,object>();
				msg["args"]=clone.ReadArray(already1=>DynamicData.Read(clone,already1),already)??[];
			} catch(Exception e){
				msg["argsError"]=e;
			}


			foreach(var context in Listening){
				context.SendMessage(msg);
			}
		}
	}

	internal static Action? Broadcast(string? type,string? method,object?[] args){
		lock(Listening){
			if(Listening.Count==0) return null;

			var buff=new DataOutputBuff();
			var toFree=new List<object>();

			try{
				var writeAlready=new Dictionary<object,int>();
				buff.WriteArray(args,(d,already)=>DynamicData.Write(buff,d,already),writeAlready);
				toFree.AddRange(writeAlready.Keys.Where(DynamicData.NeedsFreeing));
			} catch(Exception){
				Broadcast(type,method,(byte[]?)null);
				return null;
			}

			Broadcast(type,method,buff.ToByteArray());
			return ()=>DynamicData.Free(toFree);
		}
	}

	internal static void Broadcast(string? type,string? method,DataOutputBuff buff,int len){
		lock(Listening){
			if(Listening.Count==0) return;
			Broadcast(type,method,buff.ToByteArray(len));
		}
	}

	private static void Broadcast(string? type,string? method,byte[]? args){
		var msg=new StringMap<object?>{
			{"name",Rpc.Name},
			{"id",Rpc.Id},
			{"prettyName",Rpc.PrettyName},
			{"type",type},
			{"method",method},
			{"args",args},
		};

		foreach(var context in Listening){
			context.SendMessage(msg);
		}
	}
}