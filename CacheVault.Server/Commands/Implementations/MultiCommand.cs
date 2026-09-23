using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class MultiCommand : IRedisCommand {
    public string Name => "MULTI";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        ArgumentNullException.ThrowIfNull(
            context);

        if (arguments.Count != 0) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'multi' command");
        }

        if (context.Session.IsInTransaction) {
            throw new CommandArgumentException(
                "ERR MULTI calls can not be nested");
        }

        context.Session.BeginTransaction();

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("OK"));
    }
}