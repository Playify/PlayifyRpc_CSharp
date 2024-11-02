namespace PlayifyRpc.Internal.Utils;

internal static class Helpers{//TODO update PlayifyUtility and load this from there
	public static bool TryCast<T,TResult>(this T? t,out TResult result) where TResult : T{
		if(t is TResult res){
			result=res;
			return true;
		}
		result=default!;
		return false;
	}
}