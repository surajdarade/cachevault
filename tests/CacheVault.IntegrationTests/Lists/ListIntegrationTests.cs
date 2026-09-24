using CacheVault.IntegrationTests.Infrastructure;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.IntegrationTests.Lists;

public sealed class ListIntegrationTests : IAsyncLifetime {
    private CacheVaultTestServer _server = null!;

    public async Task InitializeAsync() {
        _server = new CacheVaultTestServer();
        await _server.StartAsync();
    }

    public async Task DisposeAsync() {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task LpushAndRpush_CreateExpectedList() {
        await using RedisTestClient client =
            await RedisTestClient.ConnectAsync(_server.LocalEndpoint);

        Assert.Equal(
            new RespInteger(2),
            await client.SendCommandAsync(
                "LPUSH",
                "numbers",
                "one",
                "two"));

        Assert.Equal(
            new RespInteger(3),
            await client.SendCommandAsync(
                "RPUSH",
                "numbers",
                "three"));

        AssertRespArray(
            await client.SendCommandAsync(
                "LRANGE",
                "numbers",
                "0",
                "-1"),
            new RespBulkString("two"),
            new RespBulkString("one"),
            new RespBulkString("three"));
    }

    [Fact]
    public async Task LpopAndRpop_RemoveExpectedElements() {
        await using RedisTestClient client =
            await RedisTestClient.ConnectAsync(_server.LocalEndpoint);

        await client.SendCommandAsync(
            "RPUSH",
            "numbers",
            "one",
            "two",
            "three");

        Assert.Equal(
            new RespBulkString("one"),
            await client.SendCommandAsync(
                "LPOP",
                "numbers"));

        Assert.Equal(
            new RespBulkString("three"),
            await client.SendCommandAsync(
                "RPOP",
                "numbers"));

        AssertRespArray(
            await client.SendCommandAsync(
                "LRANGE",
                "numbers",
                "0",
                "-1"),
            new RespBulkString("two"));
    }

    [Fact]
    public async Task EmptyList_ReturnsNullPopAndEmptyRange() {
        await using RedisTestClient client =
            await RedisTestClient.ConnectAsync(_server.LocalEndpoint);

        Assert.Equal(
            new RespBulkString(null),
            await client.SendCommandAsync(
                "LPOP",
                "missing"));

        AssertRespArray(
            await client.SendCommandAsync(
                "LRANGE",
                "missing",
                "0",
                "-1"));
    }

    [Fact]
    public async Task Range_SupportsNegativeIndexes() {
        await using RedisTestClient client =
            await RedisTestClient.ConnectAsync(_server.LocalEndpoint);

        await client.SendCommandAsync(
            "RPUSH",
            "numbers",
            "zero",
            "one",
            "two",
            "three");

        AssertRespArray(
            await client.SendCommandAsync(
                "LRANGE",
                "numbers",
                "1",
                "-2"),
            new RespBulkString("one"),
            new RespBulkString("two"));
    }

    [Fact]
    public async Task StringKeyAndListKeyCannotShareSameType() {
        await using RedisTestClient client =
            await RedisTestClient.ConnectAsync(_server.LocalEndpoint);

        await client.SendCommandAsync(
            "SET",
            "key",
            "value");

        RespValue error =
            await client.SendCommandAsync(
                "LPUSH",
                "key",
                "item");

        Assert.IsType<RespError>(error);

        await client.SendCommandAsync(
            "LPUSH",
            "list",
            "item");

        await client.SendCommandAsync(
            "SET",
            "list",
            "value");

        Assert.Equal(
            new RespBulkString("value"),
            await client.SendCommandAsync(
                "GET",
                "list"));
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