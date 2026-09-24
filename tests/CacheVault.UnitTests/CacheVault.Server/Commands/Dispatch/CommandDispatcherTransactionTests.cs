using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;
using CacheVault.UnitTests.CacheVault.Replication;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Dispatch;

public sealed class CommandDispatcherTransactionTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly InMemoryKeyValueStore _store;
    private readonly CommandDispatcher _dispatcher;
    private readonly ClientSession _session;
    private readonly CommandContext _context;

    public CommandDispatcherTransactionTests() {
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
    public async Task DispatchAsync_CommandOutsideTransaction_ExecutesImmediately() {
        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("SET"),
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.False(
            _session.IsInTransaction);

        Assert.Empty(
            _session.QueuedCommands);

        Assert.True(
            _store.TryGet(
                "key",
                out var value));

        Assert.NotNull(value);

        Assert.Equal(
            "value",
            value.Value);
    }

    [Fact]
    public async Task DispatchAsync_Multi_ExecutesImmediately() {
        RespValue result =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("MULTI")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.True(
            _session.IsInTransaction);

        Assert.Empty(
            _session.QueuedCommands);
    }

    [Fact]
    public async Task DispatchAsync_CommandAfterMulti_ReturnsQueued() {
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
                    new RespBulkString("SET"),
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]));

        Assert.Equal(
            new RespSimpleString("QUEUED"),
            result);

        Assert.True(
            _session.IsInTransaction);

        Assert.Single(
            _session.QueuedCommands);

        Assert.False(
            _store.TryGet(
                "key",
                out _));
    }

    [Fact]
    public async Task DispatchAsync_MultipleCommandsAfterMulti_PreservesOrder() {
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

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("GET"),
                new RespBulkString("key")
            ]));

        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("INCR"),
                new RespBulkString("counter")
            ]));

        IReadOnlyList<QueuedCommand> queuedCommands =
            _session.QueuedCommands;

        Assert.Equal(
            3,
            queuedCommands.Count);

        Assert.Equal(
            "SET",
            queuedCommands[0].Name);

        Assert.Equal(
            "GET",
            queuedCommands[1].Name);

        Assert.Equal(
            "INCR",
            queuedCommands[2].Name);
    }

    [Fact]
    public async Task DispatchAsync_QueuedCommand_PreservesArguments() {
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

        QueuedCommand queuedCommand =
            Assert.Single(
                _session.QueuedCommands);

        Assert.Equal(
            "SET",
            queuedCommand.Name);

        Assert.Collection(
            queuedCommand.Arguments,
            argument =>
            {
                var value =
                    Assert.IsType<RespBulkString>(
                        argument);

                Assert.Equal(
                    "name",
                    value.Value);
            },
            argument =>
            {
                var value =
                    Assert.IsType<RespBulkString>(
                        argument);

                Assert.Equal(
                    "Suraj",
                    value.Value);
            });
    }

    [Fact]
    public async Task DispatchAsync_Multi_IsNotQueued() {
        await _dispatcher.DispatchAsync(
            _context,
            new RespArray(
            [
                new RespBulkString("MULTI")
            ]));

        Assert.Empty(
            _session.QueuedCommands);
    }

    [Fact]
    public async Task DispatchAsync_Discard_ClearsTransactionWithoutExecutingQueuedCommands() {
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

        RespValue discardResult =
            await _dispatcher.DispatchAsync(
                _context,
                new RespArray(
                [
                    new RespBulkString("DISCARD")
                ]));

        Assert.Equal(
            new RespSimpleString("OK"),
            discardResult);

        Assert.False(
            _context.Session.IsInTransaction);

        Assert.Empty(
            _context.Session.QueuedCommands);

        Assert.False(
            _store.TryGet(
                "key",
                out _));
    }
}