namespace CacheVault.Server.Networking.Connections;

public sealed class ClientSession {
    public Guid ConnectionId { get; } = Guid.NewGuid();
}