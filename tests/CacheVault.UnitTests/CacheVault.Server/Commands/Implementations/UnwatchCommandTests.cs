using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Core.Storage;
using CacheVault.Infrastructure.Time;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.Server.Commands.Implementations;

public sealed class UnwatchCommandTests {
    private readonly IKeyValueStore _store;
    private readonly TestClock _clock;
    private readonly UnwatchCommand _command;
    private readonly ClientSession _session;
    private readonly CommandContext _context;

    public UnwatchCommandTests() {
        _clock =
            new TestClock(
                DateTimeOffset.UtcNow);

        _store =
            new InMemoryKeyValueStore(
                _clock);

        _command =
            new UnwatchCommand();

        _session =
            new ClientSession();

        _context =
            new CommandContext(
                _session,
                CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutWatchedKeys_ReturnsOk() {
        RespValue result =
            await _command.ExecuteAsync(
                _context,
                []);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithOneWatchedKey_ClearsWatchState() {
        _session.WatchState.Watch(
            "key",
            1);

        Assert.True(
            _session.WatchState.HasWatchedKeys);

        RespValue result =
            await _command.ExecuteAsync(
                _context,
                []);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.False(
            _session.WatchState.HasWatchedKeys);

        Assert.Empty(
            _session.WatchState.WatchedKeys);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleWatchedKeys_ClearsAllWatchedKeys() {
        _session.WatchState.Watch(
            "key1",
            1);

        _session.WatchState.Watch(
            "key2",
            2);

        _session.WatchState.Watch(
            "key3",
            3);

        Assert.Equal(
            3,
            _session.WatchState.WatchedKeys.Count);

        RespValue result =
            await _command.ExecuteAsync(
                _context,
                []);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.False(
            _session.WatchState.HasWatchedKeys);

        Assert.Empty(
            _session.WatchState.WatchedKeys);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotModifyStore() {
        _store.Set(
            "key",
            "value");

        long versionBefore =
            _store.GetVersion(
                "key");

        RespValue result =
            await _command.ExecuteAsync(
                _context,
                []);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.True(
            _store.TryGet(
                "key",
                out StoredValue? storedValue));

        Assert.NotNull(
            storedValue);

        Assert.Equal(
            "value",
            storedValue.Value);

        Assert.Equal(
            versionBefore,
            _store.GetVersion(
                "key"));
    }

    [Fact]
    public async Task ExecuteAsync_OutsideTransaction_ReturnsOk() {
        Assert.False(
            _session.IsInTransaction);

        RespValue result =
            await _command.ExecuteAsync(
                _context,
                []);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.False(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStartTransaction() {
        Assert.False(
            _session.IsInTransaction);

        await _command.ExecuteAsync(
            _context,
            []);

        Assert.False(
            _session.IsInTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsWatchedKeys_WithoutClearingTransactionState() {
        _session.WatchState.Watch(
            "key",
            1);

        _session.BeginTransaction();

        Assert.True(
            _session.IsInTransaction);

        Assert.True(
            _session.WatchState.HasWatchedKeys);

        RespValue result =
            await _command.ExecuteAsync(
                _context,
                []);

        RespSimpleString response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.True(
            _session.IsInTransaction);

        Assert.False(
            _session.WatchState.HasWatchedKeys);

        Assert.Empty(
            _session.WatchState.WatchedKeys);
    }

    [Fact]
    public async Task ExecuteAsync_WithArguments_ThrowsCommandArgumentException() {
        RespValue[] arguments =
        [
            new RespBulkString("key")
        ];

        CommandArgumentException exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await _command.ExecuteAsync(
                        _context,
                        arguments));

        Assert.Equal(
            "ERR wrong number of arguments for 'unwatch' command",
            exception.Message);
    }
}