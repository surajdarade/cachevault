using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class LpopCommand : IRedisCommand {
    private readonly IListStore _lists;
    private readonly IKeyValueStore _store;

    public LpopCommand(
        IListStore lists,
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(store);

        _lists = lists;
        _store = store;
    }

    public string Name => "LPOP";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'lpop' command");
        }

        string key = GetKey(arguments[0]);
        EnsureListType(key);
        string? value = _lists.PopLeft(key);

        return ValueTask.FromResult<RespValue>(
            new RespBulkString(value));
    }

    private static string GetKey(RespValue value) {
        if (value is not RespBulkString bulk || bulk.Value is null) {
            throw new CommandArgumentException("ERR invalid key");
        }

        return bulk.Value;
    }

    private void EnsureListType(string key) {
        if (!_lists.Contains(key) && _store.Contains(key)) {
            throw new CommandArgumentException(
                "WRONGTYPE Operation against a key holding the wrong kind of value");
        }
    }
}
