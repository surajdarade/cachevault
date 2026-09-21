using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class PingCommand : IRedisCommand {
    public string Name => "PING";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count > 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'ping' command");
        }

        if (arguments.Count == 1) {
            if (arguments[0] is not RespBulkString message ||
                message.Value is null) {
                throw new CommandArgumentException(
                    "ERR invalid argument");
            }

            return ValueTask.FromResult<RespValue>(
                new RespBulkString(message.Value));
        }

        return ValueTask.FromResult<RespValue>(
            new RespSimpleString("PONG"));
    }
}