using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class RespStreamReaderFactory : IRespStreamReaderFactory {
    private readonly IRespParser _parser;

    public RespStreamReaderFactory(
        IRespParser parser) {
        ArgumentNullException.ThrowIfNull(
            parser);

        _parser = parser;
    }

    public IRespStreamReader Create() {
        return new RespStreamReader(
            _parser);
    }
}