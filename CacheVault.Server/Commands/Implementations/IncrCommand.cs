using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class IncrCommand : IRedisCommand {
    private const string InvalidIntegerError =
        "ERR value is not an integer or out of range";

    private readonly IKeyValueStore _store;

    public IncrCommand(
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(
            store);

        _store = store;
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