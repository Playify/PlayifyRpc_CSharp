namespace PlayifyRpc.Connections;

internal enum PacketType:byte{
	FunctionCall=0,
	FunctionSuccess=1,
	FunctionError=2,
	FunctionCancel=3,
	MessageToExecutor=4,
	MessageToCaller=5,
}