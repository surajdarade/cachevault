using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;
using CacheVault.UnitTests.CacheVault.Replication;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class WatchCommandTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly InMemoryKeyValueStore _store;
    private readonly CommandDispatcher _dispatcher;
    private readonly ClientSession _session;
    private readonly CommandContext _context;

    public WatchCommandTests() {
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
    public async Task ExecuteAsync_WithOneKey_ReturnsOk() {
        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("WATCH"),
                    new RespBulkString("key")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_WithOneKey_StoresCurrentVersion() {
        _store.Set(
            "key",
            "value");

        long version =
            _store.GetVersion(
                "key");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
                new RespBulkString("key")
            ]));

        Assert.True(
            _session.WatchState.HasWatchedKeys);

        Assert.Equal(
            version,
            _session.WatchState.WatchedKeys["key"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingKey_StoresZeroVersion() {
        long version =
            _store.GetVersion(
                "missing");

        Assert.Equal(
            0,
            version);

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("WATCH"),
                    new RespBulkString("missing")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.Equal(
            0,
            _session.WatchState.WatchedKeys["missing"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleKeys_StoresAllVersions() {
        _store.Set(
            "first",
            "value1");

        _store.Set(
            "second",
            "value2");

        long firstVersion =
            _store.GetVersion(
                "first");

        long secondVersion =
            _store.GetVersion(
                "second");

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("WATCH"),
                    new RespBulkString("first"),
                    new RespBulkString("second")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.Equal(
            2,
            _session.WatchState.WatchedKeys.Count);

        Assert.Equal(
            firstVersion,
            _session.WatchState.WatchedKeys["first"]);

        Assert.Equal(
            secondVersion,
            _session.WatchState.WatchedKeys["second"]);
    }

    [Fact]
    public async Task ExecuteAsync_RewatchingKey_UpdatesObservedVersion() {
        _store.Set(
            "key",
            "value1");

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
                new RespBulkString("key")
            ]));

        long firstVersion =
            _session.WatchState.WatchedKeys["key"];

        _store.Set(
            "key",
            "value2");

        long secondVersion =
            _store.GetVersion(
                "key");

        Assert.True(
            secondVersion > firstVersion);

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
                new RespBulkString("key")
            ]));

        Assert.Equal(
            secondVersion,
            _session.WatchState.WatchedKeys["key"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoKeys_ThrowsError() {
        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                {
                    await _dispatcher.DispatchAsync(
                        _context,
                        new RespArray(
                        [
                            new RespBulkString("WATCH")
                        ]));
                });

        Assert.Equal(
            "ERR wrong number of arguments for 'watch' command",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullKey_ThrowsError() {
        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                {
                    await _dispatcher.DispatchAsync(
                        _context,
                        new RespArray(
                        [
                            new RespBulkString("WATCH"),
                        new RespBulkString(null)
                        ]));
                });

        Assert.Equal(
            "ERR invalid key",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonBulkStringKey_ThrowsError() {
        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                {
                    await _dispatcher.DispatchAsync(
                        _context,
                        new RespArray(
                        [
                            new RespBulkString("WATCH"),
                        new RespInteger(1)
                        ]));
                });

        Assert.Equal(
            "ERR invalid key",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCreateMissingKey() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("WATCH"),
                new RespBulkString("missing")
            ]));

        Assert.False(
            _store.Contains("missing"));

        Assert.True(
            _session.WatchState.HasWatchedKeys);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStartTransaction() {
        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("WATCH"),
                    new RespBulkString("key")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.False(
            _session.IsInTransaction);

        Assert.Empty(
            _session.QueuedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_AfterMutation_CapturesVersionAtExecutionTime() {
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

        long watchedVersion =
            _session.WatchState.WatchedKeys["key"];

        _store.Set(
            "key",
            "updated");

        long currentVersion =
            _store.GetVersion(
                "key");

        Assert.True(
            currentVersion > watchedVersion);

        Assert.Equal(
            watchedVersion,
            _session.WatchState.WatchedKeys["key"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidArgument_DoesNotPartiallyModifyWatchState() {
        _session.WatchState.Watch(
            "existing",
            10);

        RespArray request =
            new(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("new-key"),
            new RespInteger(123)
            ]);

        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await _dispatcher.DispatchAsync(
                        _context,
                        request));

        Assert.Equal(
            "ERR invalid key",
            exception.Message);

        Assert.Single(
            _session.WatchState.WatchedKeys);

        Assert.True(
            _session.WatchState.WatchedKeys.ContainsKey(
                "existing"));

        Assert.Equal(
            10,
            _session.WatchState.WatchedKeys["existing"]);

        Assert.False(
            _session.WatchState.WatchedKeys.ContainsKey(
                "new-key"));
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleValidKeys_UpdatesWatchStateOnlyAfterValidation() {
        RespArray request =
            new(
            [
                new RespBulkString("WATCH"),
            new RespBulkString("key1"),
            new RespBulkString("key2"),
            new RespBulkString("key3")
            ]);

        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                request);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.Equal(
            3,
            _session.WatchState.WatchedKeys.Count);

        Assert.Equal(
            _store.GetVersion("key1"),
            _session.WatchState.WatchedKeys["key1"]);

        Assert.Equal(
            _store.GetVersion("key2"),
            _session.WatchState.WatchedKeys["key2"]);

        Assert.Equal(
            _store.GetVersion("key3"),
            _session.WatchState.WatchedKeys["key3"]);
    }
}