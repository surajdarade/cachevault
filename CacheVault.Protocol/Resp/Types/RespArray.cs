namespace CacheVault.Protocol.Resp.Types;

public sealed record RespArray(
    IReadOnlyList<RespValue>? Values)
    : RespValue(RespValueType.Array);