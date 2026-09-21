using CacheVault.Protocol.Rest.Types;

namespace CacheVault.Protocol.Resp.Types;

public sealed record RespSimpleString(string Value)
    : RespValue(RespValueType.SimpleString);