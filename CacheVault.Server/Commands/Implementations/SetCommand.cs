using System.Globalization;
using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class SetCommand : IRedisCommand {
    private readonly IKeyValueStore _store;
    private readonly IClock _clock;

    public SetCommand(
        IKeyValueStore store,
        IClock clock) {
        ArgumentNullException.ThrowIfNull(
            store);

        ArgumentNullException.ThrowIfNull(
            clock);

        _store = store;
        _clock = clock;
    }

    public string Name => "SET";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 2 &&
            arguments.Count != 4) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'set' command");
        }

        if (arguments[0] is not RespBulkString key ||
            key.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid key");
        }

        if (arguments[1] is not RespBulkString value ||
            value.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid value");
        }

        DateTimeOffset? expiresAt = null;

        if (arguments.Count == 4) {
            expiresAt =
                ParseExpiration(
                    arguments[2],
                    arguments[3]);
        }

        _store.Set(
            key.Value,
            value.Value,
            expiresAt);

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("OK"));
    }

    private DateTimeOffset ParseExpiration(
        RespValue option,
        RespValue expiration) {
        if (option is not RespBulkString optionString ||
            optionString.Value is null) {
            throw new CommandArgumentException(
                "ERR syntax error");
        }

        if (expiration is not RespBulkString expirationString ||
            expirationString.Value is null) {
            throw new CommandArgumentException(
                "ERR syntax error");
        }

        if (!long.TryParse(
                expirationString.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long amount)) {
            throw new CommandArgumentException(
                "ERR invalid expire time in 'set' command");
        }

        if (amount <= 0) {
            throw new CommandArgumentException(
                "ERR invalid expire time in 'set' command");
        }

        try {
            if (optionString.Value.Equals(
                    "EX",
                    StringComparison.OrdinalIgnoreCase)) {
                return _clock.UtcNow.AddSeconds(
                    amount);
            }

            if (optionString.Value.Equals(
                    "PX",
                    StringComparison.OrdinalIgnoreCase)) {
                return _clock.UtcNow.AddMilliseconds(
                    amount);
            }
        }
        catch (ArgumentOutOfRangeException) {
            throw new CommandArgumentException(
                "ERR invalid expire time in 'set' command");
        }

        throw new CommandArgumentException(
            "ERR syntax error");
    }
}