using System.Globalization;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class LrangeCommand : IRedisCommand {
    private readonly IListStore _lists;
    private readonly IKeyValueStore _store;

    public LrangeCommand(
        IListStore lists,
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(store);

        _lists = lists;
        _store = store;
    }

    public string Name => "LRANGE";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 3) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'lrange' command");
        }

        string key = GetString(arguments[0], "key");
        EnsureListType(key);
        long start = ParseIndex(arguments[1]);
        long stop = ParseIndex(arguments[2]);

        IReadOnlyList<string> values =
            _lists.Range(key, start, stop);

        return ValueTask.FromResult<RespValue>(
            new RespArray(
                values
                    .Select(value => (RespValue)new RespBulkString(value))
                    .ToArray()));
    }

    private static string GetString(
        RespValue value,
        string name) {
        if (value is not RespBulkString bulk || bulk.Value is null) {
            throw new CommandArgumentException($"ERR invalid {name}");
        }

        return bulk.Value;
    }

    private static long ParseIndex(RespValue value) {
        string text = GetString(value, "index");

        if (!long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long index)) {
            throw new CommandArgumentException(
                "ERR value is not an integer or out of range");
        }

        return index;
    }

    private void EnsureListType(string key) {
        if (!_lists.Contains(key) && _store.Contains(key)) {
            throw new CommandArgumentException(
                "WRONGTYPE Operation against a key holding the wrong kind of value");
        }
    }
}
