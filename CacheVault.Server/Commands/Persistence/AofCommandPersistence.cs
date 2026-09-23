using System.Globalization;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Persistence.Aof.Writing;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Persistence;

public sealed class AofCommandPersistence : ICommandPersistence {
    private readonly AofWriter _writer;
    private readonly IKeyValueStore _store;

    public AofCommandPersistence(
        AofWriter writer,
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(
            writer);

        ArgumentNullException.ThrowIfNull(
            store);

        _writer = writer;
        _store = store;
    }

    public ValueTask PersistAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        if (!commandName.Equals(
                "SET",
                StringComparison.OrdinalIgnoreCase)) {
            return _writer.AppendCommandAsync(
                commandName,
                arguments,
                cancellationToken);
        }

        return PersistSetAsync(
            arguments,
            cancellationToken);
    }

    private async ValueTask PersistSetAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {
        if (arguments.Count < 2) {
            throw new ArgumentException(
                "SET persistence requires at least a key and value.",
                nameof(arguments));
        }

        string key = arguments[0];

        if (!_store.TryGet(
                key,
                out StoredValue? value)) {
            await _writer.AppendCommandAsync(
                "DEL",
                [key],
                cancellationToken);

            return;
        }

        if (value!.ExpiresAt is null) {
            await _writer.AppendCommandAsync(
                "SET",
                arguments,
                cancellationToken);

            return;
        }

        long expirationMilliseconds =
            value.ExpiresAt.Value.ToUnixTimeMilliseconds();

        await _writer.AppendCommandAsync(
            "SET",
            [
                key,
                arguments[1],
                "PXAT",
                expirationMilliseconds.ToString(
                    CultureInfo.InvariantCulture)
            ],
            cancellationToken);
    }
}