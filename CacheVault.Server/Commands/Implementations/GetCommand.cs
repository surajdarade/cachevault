using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class GetCommand : IRedisCommand {
    private readonly IKeyValueStore _store;

    public GetCommand(IKeyValueStore store) {
        _store = store;
    }

    public string Name => "GET";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'get' command");
        }

        if (arguments[0] is not RespBulkString key ||
            key.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid key");
        }

        if (!_store.TryGet(
                key.Value,
                out var value)) {
            return ValueTask.FromResult<RespValue>(
                new RespBulkString(null));
        }

        return ValueTask.FromResult<RespValue>(
            new RespBulkString(value!.Value));
    }
}