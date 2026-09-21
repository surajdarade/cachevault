namespace CacheVault.Protocol.Exceptions;

public sealed class RespIncompleteException : Exception {
    public RespIncompleteException()
        : base("RESP input is incomplete.") {
    }
}