using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Networking.Connections;
using CacheVault.Server.PubSub;

namespace CacheVault.UnitTests.CacheVault.Server.PubSub;

public sealed class PubSubManagerTests {
    [Fact]
    public void Subscribe_AddsChannelToSession() {
        var manager = new PubSubManager();
        var session = new ClientSession();

        manager.Subscribe(session, "news");

        Assert.True(session.IsSubscribed);
        Assert.Equal(1, session.SubscriptionCount);
        Assert.Contains("news", session.GetSubscriptions());
    }

    [Fact]
    public void Publish_DeliversMessageToAllSubscribers() {
        var manager = new PubSubManager();
        var first = new ClientSession();
        var second = new ClientSession();

        manager.Subscribe(first, "news");
        manager.Subscribe(second, "news");

        int delivered =
            manager.Publish(
                "news",
                "hello");

        Assert.Equal(2, delivered);

        RespValue firstMessage =
            first.OutboundMessages.TryRead(
                out RespValue? message)
                    ? message
                    : null!;

        AssertRespArray(
            firstMessage,
            new RespBulkString("message"),
            new RespBulkString("news"),
            new RespBulkString("hello"));

        Assert.True(
            second.OutboundMessages.TryRead(
                out _));
    }

    [Fact]
    public void Unsubscribe_RemovesOnlyRequestedChannel() {
        var manager = new PubSubManager();
        var session = new ClientSession();

        manager.Subscribe(session, "one");
        manager.Subscribe(session, "two");

        Assert.True(
            manager.Unsubscribe(
                session,
                "one"));

        Assert.Equal(
            1,
            session.SubscriptionCount);

        Assert.Equal(
            ["two"],
            session.GetSubscriptions());
    }

    [Fact]
    public void RemoveSession_RemovesAllSubscriptions() {
        var manager = new PubSubManager();
        var session = new ClientSession();

        manager.Subscribe(session, "one");
        manager.Subscribe(session, "two");

        manager.RemoveSession(session);

        Assert.False(session.IsSubscribed);
        Assert.Equal(0, session.SubscriptionCount);

        Assert.Equal(
            0,
            manager.Publish(
                "one",
                "hello"));

        Assert.Equal(
            0,
            manager.Publish(
                "two",
                "hello"));
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

        for (int index = 0;
             index < expected.Length;
             index++) {
            Assert.Equal(
                expected[index],
                actualArray.Values[index]);
        }
    }
}