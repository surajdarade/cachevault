using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Protocol.Resp.Types;

public sealed record RespError(string Message)
    : RespValue(RespValueType.Error);