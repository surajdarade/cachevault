using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Protocol.Resp.Types;

public sealed record RespInteger(long Value)
    : RespValue(RespValueType.Integer);