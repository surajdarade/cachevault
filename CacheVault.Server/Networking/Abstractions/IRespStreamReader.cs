using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Server.Networking.Abstractions;

public interface IRespStreamReader {
    ValueTask<RespValue?> ReadAsync(Stream stream, CancellationToken cancellationToken);
}