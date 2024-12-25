using PlayifyRpc.Connections;
using PlayifyRpc.Internal.Data;
using PlayifyRpc.Types.Data.Objects;
using PlayifyRpc.Types.Functions;
using PlayifyRpc.Utils;
using PlayifyUtility.Streams.Data;

namespace PlayifyRpc.Internal;

internal static class ListenAllCalls{
	private static readonly RpcListenerSet Listening=[];

	internal static async Task Listen(FunctionCallContext ctx){
		using var _=Listening.Add(ctx);
		await ctx.TaskRaw;
	}

	internal static void Broadcast(ServerConnection respondTo,string? type,DataInputBuff data)=>Listening.SendLazySingle(()=>{
		var clone=data.Clone();

		var method=clone.ReadString();

		var (b,off,len)=clone.GetBufferOffsetAndLength();
		var argsBytes=new byte[len];
		Array.Copy(b,off,argsBytes,0,len);


		var msg=new StringMap{
			{"name",respondTo.Name},
			{"id",respondTo.Id},
			{"prettyName",respondTo.PrettyName},
			{"type",type},
			{"method",method},
			{"argsBytes",argsBytes},
		};

		try{
			msg.Add("args",RpcDataPrimitive.ReadArray(clone));
		} catch(Exception e){
			msg.Add("argsError",e);
		}
		return msg;
	});

	internal static void Broadcast(string? type,string? method,RpcDataPrimitive[] args)=>Listening.SendLazySingle(()=>{
		var buff=new DataOutputBuff();

		var msg=new StringMap{
			{"name",Rpc.Name},
			{"id",Rpc.Id},
			{"prettyName",Rpc.PrettyName},
			{"type",type},
			{"method",method},
			{"args",args},
		};

		try{
			var already=new Dictionary<RpcDataPrimitive,int>();
			buff.WriteArray(args,d=>d.Write(buff,already));
			msg.Add("argsBytes",buff.ToByteArray());
		} catch(Exception e){
			msg.Add("argsError",e);
		}
		return msg;
	});
}