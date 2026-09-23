using CacheVault.Core.Models;
using CacheVault.Core.Storage;
using CacheVault.Infrastructure.Time;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Dispatch;

public sealed class WatchTransactionIntegrationTests {
    private readonly TestClock _clock;
    private readonly InMemoryKeyValueStore _store;
    private readonly CommandDispatcher _dispatcher;

    public WatchTransactionIntegrationTests() {
        _clock =
            new TestClock(
                DateTimeOffset.UtcNow);

        _store =
            new InMemoryKeyValueStore(
                _clock);

        _dispatcher =
            new CommandDispatcher();

        _dispatcher.RegisterCommands(
            CommandRegistry.CreateDefaultCommands(
                _store,
                _clock,
                _dispatcher));
    }

    [Fact]
    public async Task ExecAsync_WhenAnotherClientChangesWatchedKey_AbortsTransaction() {
        ClientSession clientA =
            new();

        ClientSession clientB =
            new();

        CommandContext contextA =
            CreateContext(clientA);

        CommandContext contextB =
            CreateContext(clientB);

        await ExecuteAsync(
            contextA,
            "WATCH",
            "counter");

        await ExecuteAsync(
            contextA,
            "MULTI");

        RespValue queuedResponse =
            await ExecuteAsync(
                contextA,
                "SET",
                "counter",
                "100");

        Assert.Equal(
            "QUEUED",
            Assert.IsType<RespSimpleString>(
                queuedResponse).Value);

        await ExecuteAsync(
            contextB,
            "SET",
            "counter",
            "200");

        RespValue execResponse =
            await ExecuteAsync(
                contextA,
                "EXEC");

        Assert.Null(
            Assert.IsType<RespArray>(
                execResponse).Values);

        Assert.True(
            _store.TryGet(
                "counter",
                out StoredValue? storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "200",
            storedValue.Value);
    }

    [Fact]
    public async Task ExecAsync_WhenAnotherClientDeletesWatchedKey_AbortsTransaction() {
        _store.Set(
            "key",
            "original");

        ClientSession clientA =
            new();

        ClientSession clientB =
            new();

        CommandContext contextA =
            CreateContext(clientA);

        CommandContext contextB =
            CreateContext(clientB);

        await ExecuteAsync(
            contextA,
            "WATCH",
            "key");

        await ExecuteAsync(
            contextA,
            "MULTI");

        RespValue queuedResponse =
            await ExecuteAsync(
                contextA,
                "SET",
                "key",
                "client-a");

        Assert.Equal(
            "QUEUED",
            Assert.IsType<RespSimpleString>(
                queuedResponse).Value);

        await ExecuteAsync(
            contextB,
            "DEL",
            "key");

        RespValue execResponse =
            await ExecuteAsync(
                contextA,
                "EXEC");

        Assert.Null(
            Assert.IsType<RespArray>(
                execResponse).Values);

        Assert.False(
            _store.Contains("key"));
    }

    [Fact]
    public async Task ExecAsync_WhenAnotherClientCreatesPreviouslyMissingWatchedKey_AbortsTransaction() {
        ClientSession clientA =
            new();

        ClientSession clientB =
            new();

        CommandContext contextA =
            CreateContext(clientA);

        CommandContext contextB =
            CreateContext(clientB);

        await ExecuteAsync(
            contextA,
            "WATCH",
            "missing-key");

        await ExecuteAsync(
            contextA,
            "MULTI");

        RespValue queuedResponse =
            await ExecuteAsync(
                contextA,
                "SET",
                "missing-key",
                "client-a");

        Assert.Equal(
            "QUEUED",
            Assert.IsType<RespSimpleString>(
                queuedResponse).Value);

        await ExecuteAsync(
            contextB,
            "SET",
            "missing-key",
            "client-b");

        RespValue execResponse =
            await ExecuteAsync(
                contextA,
                "EXEC");

        Assert.Null(
            Assert.IsType<RespArray>(
                execResponse).Values);

        Assert.True(
            _store.TryGet(
                "missing-key",
                out StoredValue? storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "client-b",
            storedValue.Value);
    }

    [Fact]
    public async Task ExecAsync_WhenAnotherClientChangesDifferentKey_DoesNotAbortTransaction() {
        ClientSession clientA =
            new();

        ClientSession clientB =
            new();

        CommandContext contextA =
            CreateContext(clientA);

        CommandContext contextB =
            CreateContext(clientB);

        await ExecuteAsync(
            contextA,
            "WATCH",
            "key1");

        await ExecuteAsync(
            contextA,
            "MULTI");

        RespValue queuedResponse =
            await ExecuteAsync(
                contextA,
                "SET",
                "key1",
                "client-a");

        Assert.Equal(
            "QUEUED",
            Assert.IsType<RespSimpleString>(
                queuedResponse).Value);

        await ExecuteAsync(
            contextB,
            "SET",
            "key2",
            "client-b");

        RespValue execResponse =
            await ExecuteAsync(
                contextA,
                "EXEC");

        RespArray response =
            Assert.IsType<RespArray>(
                execResponse);

        Assert.NotNull(
            response.Values);

        Assert.Single(
            response.Values);

        Assert.Equal(
            "OK",
            Assert.IsType<RespSimpleString>(
                response.Values[0]).Value);

        Assert.True(
            _store.TryGet(
                "key1",
                out StoredValue? storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "client-a",
            storedValue.Value);
    }

    [Fact]
    public async Task ExecAsync_AfterUnwatch_DoesNotAbortWhenWatchedKeyChanges() {
        ClientSession clientA =
            new();

        ClientSession clientB =
            new();

        CommandContext contextA =
            CreateContext(clientA);

        CommandContext contextB =
            CreateContext(clientB);

        await ExecuteAsync(
            contextA,
            "WATCH",
            "key");

        await ExecuteAsync(
            contextA,
            "UNWATCH");

        await ExecuteAsync(
            contextA,
            "MULTI");

        RespValue queuedResponse =
            await ExecuteAsync(
                contextA,
                "SET",
                "key",
                "client-a");

        Assert.Equal(
            "QUEUED",
            Assert.IsType<RespSimpleString>(
                queuedResponse).Value);

        await ExecuteAsync(
            contextB,
            "SET",
            "key",
            "client-b");

        RespValue execResponse =
            await ExecuteAsync(
                contextA,
                "EXEC");

        RespArray response =
            Assert.IsType<RespArray>(
                execResponse);

        Assert.NotNull(
            response.Values);

        Assert.Single(
            response.Values);

        Assert.Equal(
            "OK",
            Assert.IsType<RespSimpleString>(
                response.Values[0]).Value);

        Assert.True(
            _store.TryGet(
                "key",
                out StoredValue? storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "client-a",
            storedValue.Value);
    }

    private CommandContext CreateContext(
        ClientSession session) {
        return new CommandContext(
            session,
            CancellationToken.None);
    }

    private async ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        string command,
        params string[] arguments) {
        var values =
            new List<RespValue>
            {
                new RespBulkString(command)
            };

        foreach (string argument in arguments) {
            values.Add(
                new RespBulkString(argument));
        }

        var request =
            new RespArray(values);

        return await _dispatcher.DispatchAsync(
            context,
            request);
    }
}