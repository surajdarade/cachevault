using CacheVault.IntegrationTests.Infrastructure;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.IntegrationTests.Networking;

public sealed class RedisTcpServerTests {
    [Fact]
    public async Task Server_AcceptsTcpConnection() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task Ping_ReturnsPong() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue response =
            await client.SendCommandAsync(
                "PING");

        var pong =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "PONG",
            pong.Value);
    }

    [Fact]
    public async Task Echo_ReturnsProvidedMessage() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue response =
            await client.SendCommandAsync(
                "ECHO",
                "hello");

        var value =
            Assert.IsType<RespBulkString>(
                response);

        Assert.Equal(
            "hello",
            value.Value);
    }

    [Fact]
    public async Task Set_ReturnsOk() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue response =
            await client.SendCommandAsync(
                "SET",
                "name",
                "Suraj");

        var value =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "OK",
            value.Value);
    }

    [Fact]
    public async Task SetThenGet_ReturnsStoredValue() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue setResponse =
            await client.SendCommandAsync(
                "SET",
                "name",
                "Suraj");

        var setResult =
            Assert.IsType<RespSimpleString>(
                setResponse);

        Assert.Equal(
            "OK",
            setResult.Value);

        RespValue getResponse =
            await client.SendCommandAsync(
                "GET",
                "name");

        var value =
            Assert.IsType<RespBulkString>(
                getResponse);

        Assert.Equal(
            "Suraj",
            value.Value);
    }

    [Fact]
    public async Task GetMissingKey_ReturnsNullBulkString() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue response =
            await client.SendCommandAsync(
                "GET",
                "does-not-exist");

        var value =
            Assert.IsType<RespBulkString>(
                response);

        Assert.Null(
            value.Value);
    }

    [Fact]
    public async Task Del_RemovesExistingKey() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await client.SendCommandAsync(
            "SET",
            "name",
            "Suraj");

        RespValue deleteResponse =
            await client.SendCommandAsync(
                "DEL",
                "name");

        var deleted =
            Assert.IsType<RespInteger>(
                deleteResponse);

        Assert.Equal(
            1,
            deleted.Value);

        RespValue getResponse =
            await client.SendCommandAsync(
                "GET",
                "name");

        var value =
            Assert.IsType<RespBulkString>(
                getResponse);

        Assert.Null(
            value.Value);
    }

    [Fact]
    public async Task Del_MultipleKeys_ReturnsDeletedCount() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await client.SendCommandAsync(
            "SET",
            "key1",
            "value1");

        await client.SendCommandAsync(
            "SET",
            "key2",
            "value2");

        RespValue response =
            await client.SendCommandAsync(
                "DEL",
                "key1",
                "key2",
                "missing");

        var deleted =
            Assert.IsType<RespInteger>(
                response);

        Assert.Equal(
            2,
            deleted.Value);
    }

    [Fact]
    public async Task Incr_IncrementsCounter() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue firstResponse =
            await client.SendCommandAsync(
                "INCR",
                "counter");

        var firstValue =
            Assert.IsType<RespInteger>(
                firstResponse);

        Assert.Equal(
            1,
            firstValue.Value);

        RespValue secondResponse =
            await client.SendCommandAsync(
                "INCR",
                "counter");

        var secondValue =
            Assert.IsType<RespInteger>(
                secondResponse);

        Assert.Equal(
            2,
            secondValue.Value);
    }

    [Fact]
    public async Task IncrThenGet_ReturnsUpdatedValue() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await client.SendCommandAsync(
            "INCR",
            "counter");

        await client.SendCommandAsync(
            "INCR",
            "counter");

        RespValue response =
            await client.SendCommandAsync(
                "GET",
                "counter");

        var value =
            Assert.IsType<RespBulkString>(
                response);

        Assert.Equal(
            "2",
            value.Value);
    }

    [Fact]
    public async Task CommandName_IsCaseInsensitive() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue response =
            await client.SendCommandAsync(
                "ping");

        var pong =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "PONG",
            pong.Value);
    }

    [Fact]
    public async Task UnknownCommand_ReturnsError() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue response =
            await client.SendCommandAsync(
                "NOT_A_REAL_COMMAND");

        var error =
            Assert.IsType<RespError>(
                response);

        Assert.Equal(
            "ERR unknown command 'not_a_real_command'",
            error.Message);
    }

    [Fact]
    public async Task CommandError_DoesNotCloseConnection() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue errorResponse =
            await client.SendCommandAsync(
                "NOT_A_REAL_COMMAND");

        Assert.IsType<RespError>(
            errorResponse);

        RespValue pingResponse =
            await client.SendCommandAsync(
                "PING");

        var pong =
            Assert.IsType<RespSimpleString>(
                pingResponse);

        Assert.Equal(
            "PONG",
            pong.Value);
    }

    [Fact]
    public async Task PipelinedCommands_ReturnResponsesInOrder() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await client.SendRawCommandAsync(
            "PING");

        await client.SendRawCommandAsync(
            "ECHO",
            "hello");

        await client.SendRawCommandAsync(
            "PING");

        RespValue firstResponse =
            await client.ReadResponseAsync();

        RespValue secondResponse =
            await client.ReadResponseAsync();

        RespValue thirdResponse =
            await client.ReadResponseAsync();

        var first =
            Assert.IsType<RespSimpleString>(
                firstResponse);

        Assert.Equal(
            "PONG",
            first.Value);

        var second =
            Assert.IsType<RespBulkString>(
                secondResponse);

        Assert.Equal(
            "hello",
            second.Value);

        var third =
            Assert.IsType<RespSimpleString>(
                thirdResponse);

        Assert.Equal(
            "PONG",
            third.Value);
    }

    [Fact]
    public async Task FragmentedCommandAcrossTcpWrites_ReturnsCorrectResponse() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        byte[] firstPart =
            "*1\r\n$4\r\nPI"u8.ToArray();

        byte[] secondPart =
            "NG\r\n"u8.ToArray();

        await client.SendRawAsync(
            firstPart);

        await Task.Delay(10);

        await client.SendRawAsync(
            secondPart);

        RespValue response =
            await client.ReadResponseAsync();

        var pong =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "PONG",
            pong.Value);
    }

    [Fact]
    public async Task MultipleClients_ShareSameStore() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var clientA =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await using var clientB =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue setResponse =
            await clientA.SendCommandAsync(
                "SET",
                "shared-key",
                "hello");

        var setResult =
            Assert.IsType<RespSimpleString>(
                setResponse);

        Assert.Equal(
            "OK",
            setResult.Value);

        RespValue getResponse =
            await clientB.SendCommandAsync(
                "GET",
                "shared-key");

        var value =
            Assert.IsType<RespBulkString>(
                getResponse);

        Assert.Equal(
            "hello",
            value.Value);
    }

    [Fact]
    public async Task MultipleClients_ConcurrentIncrements_AreAtomic() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        const int clientCount = 10;

        const int incrementsPerClient = 100;

        var clients =
            new List<RedisTestClient>(
                clientCount);

        try {
            for (int i = 0; i < clientCount; i++) {
                RedisTestClient client =
                    await RedisTestClient.ConnectAsync(
                        server.LocalEndpoint);

                clients.Add(client);
            }

            Task[] incrementTasks =
                clients
                    .Select(
                        client =>
                            IncrementCounterAsync(
                                client,
                                incrementsPerClient))
                    .ToArray();

            await Task.WhenAll(
                incrementTasks);

            RespValue response =
                await clients[0].SendCommandAsync(
                    "GET",
                    "counter");

            var value =
                Assert.IsType<RespBulkString>(
                    response);

            Assert.Equal(
                (clientCount * incrementsPerClient).ToString(),
                value.Value);
        }
        finally {
            foreach (RedisTestClient client in clients) {
                await client.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ClientDisconnect_DoesNotAffectOtherClients() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var clientA =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await using var clientB =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue setResponse =
            await clientA.SendCommandAsync(
                "SET",
                "shared-key",
                "hello");

        var setResult =
            Assert.IsType<RespSimpleString>(
                setResponse);

        Assert.Equal(
            "OK",
            setResult.Value);

        await clientA.DisposeAsync();

        RespValue getResponse =
            await clientB.SendCommandAsync(
                "GET",
                "shared-key");

        var value =
            Assert.IsType<RespBulkString>(
                getResponse);

        Assert.Equal(
            "hello",
            value.Value);
    }

    [Fact]
    public async Task ClientDisconnect_AllowsNewClientToConnect() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using (
            RedisTestClient clientA =
                await RedisTestClient.ConnectAsync(
                    server.LocalEndpoint)) {
            RespValue response =
                await clientA.SendCommandAsync(
                    "SET",
                    "key",
                    "value");

            var result =
                Assert.IsType<RespSimpleString>(
                    response);

            Assert.Equal(
                "OK",
                result.Value);
        }

        await using var clientB =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        RespValue getResponse =
            await clientB.SendCommandAsync(
                "GET",
                "key");

        var value =
            Assert.IsType<RespBulkString>(
                getResponse);

        Assert.Equal(
            "value",
            value.Value);
    }

    [Fact]
    public async Task Server_StopAsync_CompletesWithNoActiveClients() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await server.StopAsync();
    }

    [Fact]
    public async Task Server_StopAsync_CompletesWithActiveClient() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        await using var client =
            await RedisTestClient.ConnectAsync(
                server.LocalEndpoint);

        await client.SendCommandAsync(
            "PING");

        await server.StopAsync();
    }

    [Fact]
    public async Task Server_StopAsync_CompletesWithMultipleActiveClients() {
        await using var server =
            new CacheVaultTestServer();

        await server.StartAsync();

        const int clientCount = 10;

        var clients =
            new List<RedisTestClient>(
                clientCount);

        try {
            for (int i = 0; i < clientCount; i++) {
                RedisTestClient client =
                    await RedisTestClient.ConnectAsync(
                        server.LocalEndpoint);

                clients.Add(client);

                RespValue response =
                    await client.SendCommandAsync(
                        "PING");

                var pong =
                    Assert.IsType<RespSimpleString>(
                        response);

                Assert.Equal(
                    "PONG",
                    pong.Value);
            }

            await server.StopAsync();
        }
        finally {
            foreach (RedisTestClient client in clients) {
                await client.DisposeAsync();
            }
        }
    }

    private static async Task IncrementCounterAsync(
        RedisTestClient client,
        int incrementCount) {
        for (int i = 0; i < incrementCount; i++) {
            RespValue response =
                await client.SendCommandAsync(
                    "INCR",
                    "counter");

            var result =
                Assert.IsType<RespInteger>(
                    response);

            Assert.True(
                result.Value > 0);
        }
    }
}