using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.PubSub;

namespace CacheVault.Server.Commands.Implementations;

public sealed class UnsubscribeCommand : IRedisCommand {
    private readonly PubSubManager _pubSubManager;

    public UnsubscribeCommand(PubSubManager pubSubManager) {
        ArgumentNullException.ThrowIfNull(pubSubManager);
        _pubSubManager = pubSubManager;
    }

    public string Name => "UNSUBSCRIBE";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        string[] channels =
            arguments.Count == 0
                ? context.Session.GetSubscriptions()
                : ParseChannels(arguments);

        if (channels.Length == 0) {
            return ValueTask.FromResult<RespValue>(
                CreateResponse(
                    "unsubscribe",
                    null,
                    0));
        }

        RespValue? firstResponse = null;

        foreach (string channel in channels) {
            _pubSubManager.Unsubscribe(
                context.Session,
                channel);

            RespArray response =
                CreateResponse(
                    "unsubscribe",
                    channel,
                    context.Session.SubscriptionCount);

            if (firstResponse is null) {
                firstResponse = response;
            }
            else {
                context.Session.TryEnqueue(response);
            }
        }

        return ValueTask.FromResult(
            firstResponse!);
    }

    private static string[] ParseChannels(
        IReadOnlyList<RespValue> arguments) {
        var channels =
            new string[arguments.Count];

        for (int i = 0; i < arguments.Count; i++) {
            if (arguments[i] is not RespBulkString channel ||
                channel.Value is null ||
                string.IsNullOrWhiteSpace(channel.Value)) {
                throw new CommandArgumentException(
                    "ERR invalid channel");
            }

            channels[i] = channel.Value;
        }

        return channels;
    }

    private static RespArray CreateResponse(
        string type,
        string? channel,
        int subscriptionCount) {
        return new RespArray(
            [
                new RespBulkString(type),
                new RespBulkString(channel),
                new RespInteger(subscriptionCount)
            ]);
    }
}
