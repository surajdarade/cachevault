using CacheVault.Protocol.Rest.Types;

namespace CacheVault.Protocol.Resp.Types;

public sealed record RespBulkString(string? Value)
    : RespValue(RespValueType.BulkString);