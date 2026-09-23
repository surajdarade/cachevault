using CacheVault.Persistence.Aof.Writing;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Persistence;

public sealed class AofCommandPersistence : ICommandPersistence {
    private readonly AofWriter _writer;

    public AofCommandPersistence(AofWriter writer) {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
    }

    public ValueTask PersistAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        return _writer.AppendCommandAsync(
            commandName,
            arguments,
            cancellationToken);
    }
}