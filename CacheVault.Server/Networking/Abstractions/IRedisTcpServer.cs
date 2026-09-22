using System.Net;

namespace CacheVault.Server.Networking.Abstractions;

public interface IRedisTcpServer {
    IPEndPoint? LocalEndpoint { get; }

    Task StartAsync(
        CancellationToken cancellationToken);

    Task StopAsync(
        CancellationToken cancellationToken);
}