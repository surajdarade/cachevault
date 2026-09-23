using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class UnwatchCommand : IRedisCommand {
    public string Name => "UNWATCH";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != 0) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'unwatch' command");
        }

        context.Session.WatchState.Clear();

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("OK"));
    }
}