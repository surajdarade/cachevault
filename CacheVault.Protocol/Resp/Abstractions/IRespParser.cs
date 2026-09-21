using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Protocol.Resp.Abstractions;

public interface IRespParser {
    RespValue Parse(ReadOnlySpan<byte> data);

    bool TryParse(ReadOnlySpan<byte> data, out RespValue? value, out int bytesConsumed);
}