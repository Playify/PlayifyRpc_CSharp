using PlayifyUtility.Jsons;
using PlayifyUtility.Streams.Data;
#if NET48
using PlayifyUtility.Utils.Extensions;
#endif

namespace PlayifyRpc.Types;

public partial class RpcException{
	public void Write(DataOutput output){
		output.WriteString(Type);
		output.WriteString(From);
		output.WriteString(Message);
		output.WriteString(StackTrace);
		output.WriteString(Data.Count==0?null:Data.ToString());
	}

	public static RpcException Read(DataInput input){
		var type=input.ReadString();
		var from=input.ReadString()??"???";
		var message=input.ReadString();
		var stackTrace=input.ReadString()??"";

		JsonObject? data;
		try{
			data=input.ReadString() is{} jsonString?JsonObject.ParseOrNull(jsonString):null;
		} catch(EndOfStreamException){
			data=new JsonObject{{"$info","JsonData was not included, due to an old "+nameof(PlayifyRpc)+" version"}};
		}

		return Read(type,from,message,stackTrace,data);
	}

	private static RpcException Read(string? type,string from,string? message,string stackTrace,JsonObject? data){
		var exception=data?.Get("$type")?.AsString() is{} typeTag&&Constructors.TryGetValue(typeTag,out var constructor)
			              ?(RpcException)constructor.Invoke(new object?[]{type,from,message,stackTrace})
			              :new RpcException(type,from,message,stackTrace);

		if(data!=null)
			foreach(var (key,value) in data)
				exception.Data[key]=value;

		return exception;
	}
}