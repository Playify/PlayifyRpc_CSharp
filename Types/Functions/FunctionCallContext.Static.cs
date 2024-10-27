namespace PlayifyRpc.Types.Functions;

public partial class FunctionCallContext{
	private static readonly ThreadLocal<FunctionCallContext?> ThreadLocal=new();
	[Obsolete("Use FunctionCallContext as Method parameter instead")]
	internal static T RunWithContext<T>(Func<FunctionCallContext,T> func,FunctionCallContext context){
		var old=ThreadLocal.Value;
		ThreadLocal.Value=context;
		try{
			return func(context);
		} finally{
			ThreadLocal.Value=old;
		}
	}

	[Obsolete("Use FunctionCallContext as Method parameter instead")]
	public static FunctionCallContext GetContext()=>ThreadLocal.Value??throw new InvalidOperationException("FunctionCallContext not available");
}