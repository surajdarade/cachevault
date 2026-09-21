using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Networking.Abstractions;

public interface IClientConnection {
    ClientSession Session { get; }

    Task RunAsync(CancellationToken cancellationToken);
}