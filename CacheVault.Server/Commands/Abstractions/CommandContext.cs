using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Commands.Abstractions;

public sealed class CommandContext {
    public CommandContext(
        ClientSession session,
        CancellationToken cancellationToken,
        bool isReplay = false) {
        ArgumentNullException.ThrowIfNull(
            session);

        Session = session;
        CancellationToken = cancellationToken;
        IsReplay = isReplay;
    }

    public ClientSession Session { get; }

    public CancellationToken CancellationToken { get; }

    public bool IsReplay { get; }
}