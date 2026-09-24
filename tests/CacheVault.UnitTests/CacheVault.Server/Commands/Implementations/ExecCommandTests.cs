using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;
using CacheVault.UnitTests.CacheVault.Replication;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class ExecCommandTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly InMemoryKeyValueStore _store;
    private readonly CommandDispatcher _dispatcher;
    private readonly ClientSession _session;
    private readonly CommandContext _context;

    public ExecCommandTests() {
        _store =
            new InMemoryKeyValueStore(
                _clock);

        _dispatcher =
            new CommandDispatcher();

        var replicationWaiter =
            new TestReplicationWaiter();

        _dispatcher.RegisterCommands(
            CommandRegistry.CreateDefaultCommands(
                _store,
                _clock,
                _dispatcher,
                replicationWaiter));

        _session =
            new ClientSession();

        _context =
            new CommandContext(
                _session,
                CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMulti_ThrowsError() {
        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                {
                    await _dispatcher.DispatchAsync(
                        _context,
                        new RespArray(
                        [
                            new RespBulkString("EXEC")
                        ]));
                });

        Assert.Equal(
            "ERR EXEC without MULTI",
            exception.Message);

        Assert.False(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_WithArguments_ThrowsError() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                {
                    await _dispatcher.DispatchAsync(
                        _context,
                        new RespArray(
                        [
                            new RespBulkString("EXEC"),
                        new RespBulkString("unexpected")
                        ]));
                });

        Assert.Equal(
            "ERR wrong number of arguments for 'exec' command",
            exception.Message);

        Assert.True(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyTransaction_ReturnsEmptyArray() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var response =
            Assert.IsType<RespArray>(
                result);

        Assert.NotNull(
            response.Values);

        Assert.Empty(
            response.Values);

        Assert.False(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_QueuedSet_ExecutesCommand() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        RespValue queuedResult =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("SET"),
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]));

        Assert.Equal(
            new RespSimpleString("QUEUED"),
            queuedResult);

        Assert.False(
            _store.TryGet(
                "key",
                out _));

        RespValue execResult =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var responses =
            Assert.IsType<RespArray>(
                execResult);

        Assert.NotNull(
            responses.Values);

        RespValue response =
            Assert.Single(
                responses.Values);

        Assert.Equal(
            new RespSimpleString("OK"),
            response);

        Assert.True(
            _store.TryGet(
                "key",
                out var storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "value",
            storedValue.Value);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleCommands_ReturnsResponsesInOrder() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("name"),
                new RespBulkString("Suraj")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("GET"),
                new RespBulkString("name")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("INCR"),
                new RespBulkString("counter")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var responses =
            Assert.IsType<RespArray>(
                result);

        Assert.NotNull(
            responses.Values);

        Assert.Equal(
            3,
            responses.Values.Count);

        Assert.Equal(
            new RespSimpleString("OK"),
            responses.Values[0]);

        Assert.Equal(
            new RespBulkString("Suraj"),
            responses.Values[1]);

        Assert.Equal(
            new RespInteger(1),
            responses.Values[2]);

        Assert.False(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsTransactionBeforeReturning() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("key"),
                new RespBulkString("value")
            ]));

        Assert.True(
            _session.IsInTransaction);

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("EXEC")
            ]));

        Assert.False(
            _session.IsInTransaction);

        Assert.Empty(
            _session.QueuedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_CommandError_ReturnsErrorElementAndContinues() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("counter"),
                new RespBulkString("not-an-integer")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("INCR"),
                new RespBulkString("counter")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var responses =
            Assert.IsType<RespArray>(
                result);

        Assert.NotNull(
            responses.Values);

        Assert.Equal(
            2,
            responses.Values.Count);

        Assert.Equal(
            new RespSimpleString("OK"),
            responses.Values[0]);

        var error =
            Assert.IsType<RespError>(
                responses.Values[1]);

        Assert.Equal(
            "ERR value is not an integer or out of range",
            error.Message);

        Assert.False(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_CommandError_DoesNotPreventLaterCommands() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("INCR"),
                new RespBulkString("counter")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("name"),
                new RespBulkString("Suraj")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("INCR"),
                new RespBulkString("name")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var responses =
            Assert.IsType<RespArray>(
                result);

        Assert.NotNull(
            responses.Values);

        Assert.Equal(
            3,
            responses.Values.Count);

        Assert.Equal(
            new RespInteger(1),
            responses.Values[0]);

        Assert.Equal(
            new RespSimpleString("OK"),
            responses.Values[1]);

        var error =
            Assert.IsType<RespError>(
                responses.Values[2]);

        Assert.Equal(
            "ERR value is not an integer or out of range",
            error.Message);

        Assert.True(
            _store.TryGet(
                "name",
                out var storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "Suraj",
            storedValue.Value);
    }

    [Fact]
    public async Task ExecuteAsync_DiscardedTransaction_DoesNotExecuteCommands() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("key"),
                new RespBulkString("value")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("DISCARD")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.False(
            _session.IsInTransaction);

        Assert.Empty(
            _session.QueuedCommands);

        Assert.False(
            _store.TryGet(
                "key",
                out _));
    }

    [Fact]
    public async Task ExecuteAsync_PreservesQueuedCommandArguments() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("name"),
                new RespBulkString("Suraj")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("GET"),
                new RespBulkString("name")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var responses =
            Assert.IsType<RespArray>(
                result);

        Assert.NotNull(
            responses.Values);

        Assert.Equal(
            2,
            responses.Values.Count);

        Assert.Equal(
            new RespSimpleString("OK"),
            responses.Values[0]);

        Assert.Equal(
            new RespBulkString("Suraj"),
            responses.Values[1]);
    }

    [Fact]
    public async Task ExecuteAsync_WatchedKeyUnchanged_ExecutesTransaction() {
        _store.Set(
            "key",
            "initial");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("key")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
            new RespBulkString("key"),
            new RespBulkString("updated")
            ]));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var responses =
            Assert.IsType<RespArray>(
                result);

        Assert.NotNull(
            responses.Values);

        Assert.Single(
            responses.Values);

        Assert.Equal(
            new RespSimpleString("OK"),
            responses.Values[0]);

        Assert.True(
            _store.TryGet(
                "key",
                out var value));

        Assert.NotNull(
            value);

        Assert.Equal(
            "updated",
            value.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WatchedKeyChanged_AbortsTransaction() {
        _store.Set(
            "key",
            "initial");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("key")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
            new RespBulkString("other"),
            new RespBulkString("queued-value")
            ]));

        _store.Set(
            "key",
            "changed-by-other-client");

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var response =
            Assert.IsType<RespArray>(
                result);

        Assert.Null(
            response.Values);

        Assert.False(
            _session.IsInTransaction);

        Assert.Empty(
            _session.QueuedCommands);

        Assert.False(
            _store.Contains("other"));
    }

    [Fact]
    public async Task ExecuteAsync_WatchedMissingKeyCreated_AbortsTransaction() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("missing")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
            new RespBulkString("other"),
            new RespBulkString("value")
            ]));

        _store.Set(
            "missing",
            "created");

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var response =
            Assert.IsType<RespArray>(
                result);

        Assert.Null(
            response.Values);

        Assert.False(
            _store.Contains("other"));
    }

    [Fact]
    public async Task ExecuteAsync_WatchedKeyDeleted_AbortsTransaction() {
        _store.Set(
            "key",
            "value");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("key")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
            new RespBulkString("other"),
            new RespBulkString("value")
            ]));

        Assert.True(
            _store.Remove("key"));

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var response =
            Assert.IsType<RespArray>(
                result);

        Assert.Null(
            response.Values);

        Assert.False(
            _store.Contains("other"));
    }

    [Fact]
    public async Task ExecuteAsync_WatchedKeyIncremented_AbortsTransaction() {
        _store.Set(
            "counter",
            "10");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("counter")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
            new RespBulkString("other"),
            new RespBulkString("value")
            ]));

        _store.Increment(
            "counter");

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var response =
            Assert.IsType<RespArray>(
                result);

        Assert.Null(
            response.Values);

        Assert.False(
            _store.Contains("other"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnyWatchedKeyChanges_AbortsTransaction() {
        _store.Set(
            "first",
            "value1");

        _store.Set(
            "second",
            "value2");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("first"),
            new RespBulkString("second")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("SET"),
            new RespBulkString("other"),
            new RespBulkString("value")
            ]));

        _store.Set(
            "second",
            "changed");

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("EXEC")
                ]));

        var response =
            Assert.IsType<RespArray>(
                result);

        Assert.Null(
            response.Values);

        Assert.False(
            _store.Contains("other"));
    }
}