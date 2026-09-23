using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Persistence;

public sealed class NoOpCommandPersistence : ICommandPersistence {
    public ValueTask PersistAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.CompletedTask;
    }
}