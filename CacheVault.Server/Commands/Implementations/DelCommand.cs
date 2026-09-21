using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class DelCommand : IRedisCommand {
    private readonly IKeyValueStore _store;

    public DelCommand(IKeyValueStore store) {
        _store = store;
    }

    public string Name => "DEL";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count == 0) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'del' command");
        }

        long removedCount = 0;

        foreach (RespValue argument in arguments) {
            if (argument is not RespBulkString key ||
                key.Value is null) {
                throw new CommandArgumentException(
                    "ERR invalid key");
            }

            if (_store.Remove(key.Value)) {
                removedCount++;
            }
        }

        return ValueTask.FromResult<RespValue>(
            new RespInteger(removedCount));
    }
}