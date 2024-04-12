namespace PlayifyRpc.Internal.Utils;

public static class Utility{//TODO move to a dedicated class maybe?
	public static string Quoted(string? s)=>s==null?"null":"\""+s+"\"";
}