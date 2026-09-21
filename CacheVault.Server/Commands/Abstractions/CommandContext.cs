using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Commands.Abstractions;

public sealed class CommandContext {
    public CommandContext(
        ClientSession session,
        CancellationToken cancellationToken) {
        Session = session;
        CancellationToken = cancellationToken;
    }

    public ClientSession Session { get; }

    public CancellationToken CancellationToken { get; }
}