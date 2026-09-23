using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class DiscardCommand : IRedisCommand {
    public string Name => "DISCARD";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        ArgumentNullException.ThrowIfNull(
            context);

        if (arguments.Count != 0) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'discard' command");
        }

        if (!context.Session.IsInTransaction) {
            throw new CommandArgumentException(
                "ERR DISCARD without MULTI");
        }

        context.Session.DiscardTransaction();

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("OK"));
    }
}