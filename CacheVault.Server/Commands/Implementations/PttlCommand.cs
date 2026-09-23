using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class PttlCommand : IRedisCommand {
    private readonly IKeyValueStore _store;
    private readonly IClock _clock;

    public PttlCommand(
        IKeyValueStore store,
        IClock clock) {
        ArgumentNullException.ThrowIfNull(
            store);

        ArgumentNullException.ThrowIfNull(
            clock);

        _store = store;
        _clock = clock;
    }

    public string Name => "PTTL";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'pttl' command");
        }

        if (arguments[0] is not RespBulkString key ||
            key.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid key");
        }

        if (!_store.TryGet(
                key.Value,
                out StoredValue? value)) {
            return ValueTask.FromResult<RespValue>(
                new RespInteger(-2));
        }

        if (value!.ExpiresAt is null) {
            return ValueTask.FromResult<RespValue>(
                new RespInteger(-1));
        }

        TimeSpan remaining =
            value.ExpiresAt.Value -
            _clock.UtcNow;

        if (remaining <= TimeSpan.Zero) {
            return ValueTask.FromResult<RespValue>(
                new RespInteger(-2));
        }

        long milliseconds =
            (long)Math.Floor(
                remaining.TotalMilliseconds);

        return ValueTask.FromResult<RespValue>(
            new RespInteger(milliseconds));
    }
}