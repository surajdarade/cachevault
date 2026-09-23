using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class WatchCommand : IRedisCommand {
    private readonly IKeyValueStore _store;

    public WatchCommand(
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    public string Name => "WATCH";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'watch' command");
        }

        var keys =
            new List<string>(arguments.Count);

        foreach (RespValue argument in arguments) {
            if (argument is not RespBulkString key ||
                key.Value is null) {
                throw new CommandArgumentException(
                    "ERR invalid key");
            }

            keys.Add(key.Value);
        }

        var versions =
            new Dictionary<string, long>(
                StringComparer.Ordinal);

        foreach (string key in keys) {
            versions[key] =
                _store.GetVersion(key);
        }

        foreach (KeyValuePair<string, long> entry in versions) {
            context.Session.WatchState.Watch(
                entry.Key,
                entry.Value);
        }

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("OK"));
    }
}