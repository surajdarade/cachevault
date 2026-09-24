using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Server.Networking.Abstractions;

public interface IClientConnectionHandler {
    Task HandleAsync(
        TcpClient client,
        IRespStreamReader respStreamReader,
        RespValue? initialMessage,
        CancellationToken cancellationToken);
}