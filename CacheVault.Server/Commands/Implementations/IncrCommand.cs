using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class IncrCommand : IRedisCommand {
    private const string InvalidIntegerError =
        "ERR value is not an integer or out of range";

    private readonly IKeyValueStore _store;
    private readonly IListStore? _lists;

    public IncrCommand(
        IKeyValueStore store,
        IListStore? lists = null) {
        ArgumentNullException.ThrowIfNull(
            store);

        _store = store;
        _lists = lists;
    }

    public string Name => "INCR";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'incr' command");
        }

        if (arguments[0] is not RespBulkString key ||
            key.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid key");
        }

        if (_lists?.Contains(key.Value) == true) {
            throw new CommandArgumentException(
                "WRONGTYPE Operation against a key holding the wrong kind of value");
        }

        try {
            long value =
                _store.Increment(
                    key.Value);

            return ValueTask.FromResult<RespValue>(
                new RespInteger(value));
        }
        catch (InvalidOperationException) {
            throw new CommandArgumentException(
                InvalidIntegerError);
        }
    }
}