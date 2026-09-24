using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class LlenCommand : IRedisCommand {
    private readonly IListStore _lists;
    private readonly IKeyValueStore _store;

    public LlenCommand(
        IListStore lists,
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(store);

        _lists = lists;
        _store = store;
    }

    public string Name => "LLEN";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'llen' command");
        }

        if (arguments[0] is not RespBulkString key || key.Value is null) {
            throw new CommandArgumentException("ERR invalid key");
        }

        EnsureListType(key.Value);

        return ValueTask.FromResult<RespValue>(
            new RespInteger(_lists.Length(key.Value)));
    }

    private void EnsureListType(string key) {
        if (!_lists.Contains(key) && _store.Contains(key)) {
            throw new CommandArgumentException(
                "WRONGTYPE Operation against a key holding the wrong kind of value");
        }
    }
}
