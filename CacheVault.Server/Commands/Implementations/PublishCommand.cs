using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.PubSub;

namespace CacheVault.Server.Commands.Implementations;

public sealed class PublishCommand : IRedisCommand {
    private readonly PubSubManager _pubSubManager;

    public PublishCommand(PubSubManager pubSubManager) {
        ArgumentNullException.ThrowIfNull(pubSubManager);
        _pubSubManager = pubSubManager;
    }

    public string Name => "PUBLISH";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 2) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'publish' command");
        }

        if (arguments[0] is not RespBulkString channel ||
            channel.Value is null ||
            string.IsNullOrWhiteSpace(channel.Value)) {
            throw new CommandArgumentException(
                "ERR invalid channel");
        }

        if (arguments[1] is not RespBulkString message ||
            message.Value is null) {
            throw new CommandArgumentException(
                "ERR invalid message");
        }

        int delivered =
            _pubSubManager.Publish(
                channel.Value,
                message.Value);

        return ValueTask.FromResult<RespValue>(
            new RespInteger(delivered));
    }
}
