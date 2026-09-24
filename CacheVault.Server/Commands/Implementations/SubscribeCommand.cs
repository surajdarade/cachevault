using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.PubSub;

namespace CacheVault.Server.Commands.Implementations;

public sealed class SubscribeCommand : IRedisCommand
{
    private readonly PubSubManager _pubSubManager;

    public SubscribeCommand(PubSubManager pubSubManager)
    {
        ArgumentNullException.ThrowIfNull(pubSubManager);
        _pubSubManager = pubSubManager;
    }

    public string Name => "SUBSCRIBE";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments)
    {
        if (arguments.Count == 0)
        {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'subscribe' command");
        }

        RespValue? lastResponse = null;

        for (int index = 0; index < arguments.Count; index++)
        {
            RespValue argument = arguments[index];

            if (argument is not RespBulkString channel ||
                channel.Value is null ||
                string.IsNullOrWhiteSpace(channel.Value))
            {
                throw new CommandArgumentException(
                    "ERR invalid channel");
            }

            _pubSubManager.Subscribe(
                context.Session,
                channel.Value);

            RespArray response =
                CreateResponse(
                    "subscribe",
                    channel.Value,
                    context.Session.SubscriptionCount);

            if (index < arguments.Count - 1)
            {
                context.Session.TryEnqueue(response);
            }
            else
            {
                lastResponse = response;
            }
        }

        return ValueTask.FromResult(
            lastResponse!);
    }

    private static RespArray CreateResponse(
        string type,
        string channel,
        int subscriptionCount)
    {
        return new RespArray(
            [
                new RespBulkString(type),
                new RespBulkString(channel),
                new RespInteger(subscriptionCount)
            ]);
    }
}