using PlayifyRpc.Connections;
using PlayifyRpc.Types.Data;
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

	internal static void Broadcast(ServerConnection respondTo,string? type,DataInputBuff clone){
		lock(Listening){
			if(Listening.Count==0) return;


			var msg=new StringMap<object?>{
				{"name",respondTo.Name},
				{"id",respondTo.Id},
				{"prettyName",respondTo.PrettyName},
				{"type",type},
				{"method",clone.ReadString()},
				{"args",clone.ReadAll()},
			};

			foreach(var context in Listening){
				context.SendMessage(msg);
			}
		}
	}

	internal static Action? Broadcast(string? type,string? method,object?[] args){
		lock(Listening){
			if(Listening.Count==0) return null;

			var buff=new DataOutputBuff();
			var already=new List<object>();

			try{
				buff.WriteArray(args,buff.WriteDynamic,already);
			} catch(Exception){
				Broadcast(type,method,(byte[]?)null);
				return null;
			}

			Broadcast(type,method,buff.ToByteArray());

			DynamicData.CleanupBeforeFreeing(already);
			return ()=>DynamicData.Free(already);
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