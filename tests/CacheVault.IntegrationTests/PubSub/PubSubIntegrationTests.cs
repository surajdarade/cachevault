using CacheVault.IntegrationTests.Infrastructure;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.IntegrationTests.PubSub;

public sealed class PubSubIntegrationTests : IAsyncLifetime {
    private CacheVaultTestServer _server = null!;

    public async Task InitializeAsync() {
        _server = new CacheVaultTestServer();
        await _server.StartAsync();
    }

    public async Task DisposeAsync() {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAndPublish_DeliversMessage() {
        await using RedisTestClient subscriber =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await using RedisTestClient publisher =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        RespValue subscription =
            await subscriber.SendCommandAsync(
                "SUBSCRIBE",
                "news");

        AssertRespArray(
            subscription,
            new RespBulkString("subscribe"),
            new RespBulkString("news"),
            new RespInteger(1));

        RespValue publishResult =
            await publisher.SendCommandAsync(
                "PUBLISH",
                "news",
                "hello");

        Assert.Equal(
            new RespInteger(1),
            publishResult);

        RespValue message =
            await subscriber.ReadResponseAsync();

        AssertRespArray(
            message,
            new RespBulkString("message"),
            new RespBulkString("news"),
            new RespBulkString("hello"));
    }

    [Fact]
    public async Task Subscribe_MultipleChannels_ReturnsAcknowledgementForEachChannel() {
        await using RedisTestClient subscriber =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await subscriber.SendRawCommandAsync(
            "SUBSCRIBE",
            "one",
            "two");

        RespValue first =
            await subscriber.ReadResponseAsync();

        RespValue second =
            await subscriber.ReadResponseAsync();

        AssertRespArray(
            first,
            new RespBulkString("subscribe"),
            new RespBulkString("one"),
            new RespInteger(1));

        AssertRespArray(
            second,
            new RespBulkString("subscribe"),
            new RespBulkString("two"),
            new RespInteger(2));
    }

    [Fact]
    public async Task Ping_InSubscriptionMode_ReturnsPongArray() {
        await using RedisTestClient subscriber =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await subscriber.SendCommandAsync(
            "SUBSCRIBE",
            "news");

        RespValue response =
            await subscriber.SendCommandAsync(
                "PING",
                "hello");

        AssertRespArray(
            response,
            new RespBulkString("pong"),
            new RespBulkString("hello"));
    }

    [Fact]
    public async Task MultipleSubscribers_ReceiveSameMessage() {
        await using RedisTestClient first =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await using RedisTestClient second =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await using RedisTestClient publisher =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await first.SendCommandAsync(
            "SUBSCRIBE",
            "news");

        await second.SendCommandAsync(
            "SUBSCRIBE",
            "news");

        RespValue result =
            await publisher.SendCommandAsync(
                "PUBLISH",
                "news",
                "hello");

        Assert.Equal(
            new RespInteger(2),
            result);

        RespValue firstMessage =
            await first.ReadResponseAsync();

        RespValue secondMessage =
            await second.ReadResponseAsync();

        AssertRespArray(
            firstMessage,
            new RespBulkString("message"),
            new RespBulkString("news"),
            new RespBulkString("hello"));

        AssertRespArray(
            secondMessage,
            new RespBulkString("message"),
            new RespBulkString("news"),
            new RespBulkString("hello"));
    }

    [Fact]
    public async Task Unsubscribe_StopsMessageDelivery() {
        await using RedisTestClient subscriber =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await using RedisTestClient publisher =
            await RedisTestClient.ConnectAsync(
                _server.LocalEndpoint);

        await subscriber.SendCommandAsync(
            "SUBSCRIBE",
            "news");

        RespValue unsubscribe =
            await subscriber.SendCommandAsync(
                "UNSUBSCRIBE",
                "news");

        AssertRespArray(
            unsubscribe,
            new RespBulkString("unsubscribe"),
            new RespBulkString("news"),
            new RespInteger(0));

        RespValue publishResult =
            await publisher.SendCommandAsync(
                "PUBLISH",
                "news",
                "hello");

        Assert.Equal(
            new RespInteger(0),
            publishResult);
    }

    private static void AssertRespArray(
        RespValue actual,
        params RespValue[] expected) {
        RespArray actualArray =
            Assert.IsType<RespArray>(actual);

        Assert.NotNull(actualArray.Values);

        Assert.Equal(
            expected.Length,
            actualArray.Values.Count);

        for (int index = 0; index < expected.Length; index++) {
            Assert.Equal(
                expected[index],
                actualArray.Values[index]);
        }
    }
}