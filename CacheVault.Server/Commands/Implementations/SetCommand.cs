using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class SetCommand : IRedisCommand {
    private readonly IKeyValueStore _store;

    public SetCommand(IKeyValueStore store) {
        _store = store;
    }

    public string Name => "SET";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 2) {
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

        _store.Set(
            key.Value,
            value.Value);

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("OK"));
    }
}