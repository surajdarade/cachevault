using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Implementations;

public sealed class EchoCommand : IRedisCommand {
    public string Name => "ECHO";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 1) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'echo' command");
        }

        if (arguments[0] is not RespBulkString message ||
            message.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid argument");
        }

        return ValueTask.FromResult<RespValue>(
            new RespBulkString(message.Value));
    }
}