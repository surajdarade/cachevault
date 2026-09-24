using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class LpushCommand : IRedisCommand {
    private readonly IListStore _lists;
    private readonly IKeyValueStore _store;

    public LpushCommand(
        IListStore lists,
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(store);

        _lists = lists;
        _store = store;
    }

    public string Name => "LPUSH";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count < 2) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'lpush' command");
        }

        string key = GetString(arguments[0], "key");
        EnsureListType(key);
        string[] values = GetValues(arguments, 1);

        int length = _lists.PushLeft(key, values);

        return ValueTask.FromResult<RespValue>(
            new RespInteger(length));
    }

    private static string[] GetValues(
        IReadOnlyList<RespValue> arguments,
        int start) {
        var values = new string[arguments.Count - start];

        for (int index = start; index < arguments.Count; index++) {
            values[index - start] = GetString(arguments[index], "value");
        }

        return values;
    }

    private static string GetString(
        RespValue value,
        string name) {
        if (value is not RespBulkString bulk || bulk.Value is null) {
            throw new CommandArgumentException($"ERR invalid {name}");
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
