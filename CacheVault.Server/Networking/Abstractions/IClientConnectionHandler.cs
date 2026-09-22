using System.Net.Sockets;

namespace CacheVault.Server.Networking.Abstractions;

public interface IClientConnectionHandler {
    Task HandleAsync(TcpClient client, CancellationToken cancellationToken);
}