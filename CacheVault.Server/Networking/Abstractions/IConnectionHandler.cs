using System.Net.Sockets;

namespace CacheVault.Server.Networking.Abstractions;

public interface IConnectionHandler {
    Task HandleAsync(
        TcpClient client,
        CancellationToken cancellationToken);
}