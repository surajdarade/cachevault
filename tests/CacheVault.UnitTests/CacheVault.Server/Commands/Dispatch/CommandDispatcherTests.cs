using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;
using CacheVault.Replication.Abstractions;
using CacheVault.UnitTests.CacheVault.Replication;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Dispatch;

public sealed class CommandDispatcherTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly CommandDispatcher _dispatcher;

    public CommandDispatcherTests() {
        var store =
            new InMemoryKeyValueStore(
                _clock);

        _dispatcher =
            new CommandDispatcher();

        var replicationWaiter =
            new TestReplicationWaiter();

        _dispatcher.RegisterCommands(
            CommandRegistry.CreateDefaultCommands(
                store,
                _clock,
                _dispatcher,
                replicationWaiter));
    }

    private static CommandContext CreateContext() {
        return new CommandContext(
            new ClientSession(),
            CancellationToken.None);
    }

    [Fact]
    public async Task DispatchAsync_Ping_ReturnsPong() {
        var request = new RespArray(
        [
            new RespBulkString("PING")
        ]);

        RespValue result =
            await _dispatcher.DispatchAsync(
                CreateContext(),
                request);

        var response =
            Assert.IsType<RespSimpleString>(result);

        Assert.Equal("PONG", response.Value);
    }

    [Fact]
    public async Task DispatchAsync_PingWithMessage_ReturnsMessage() {
        var request = new RespArray(
        [
            new RespBulkString("PING"),
            new RespBulkString("hello")
        ]);

        RespValue result =
            await _dispatcher.DispatchAsync(
                CreateContext(),
                request);

        var response =
            Assert.IsType<RespBulkString>(result);

        Assert.Equal("hello", response.Value);
    }

    [Fact]
    public async Task DispatchAsync_Echo_ReturnsMessage() {
        var request = new RespArray(
        [
            new RespBulkString("ECHO"),
            new RespBulkString("hello")
        ]);

        RespValue result =
            await _dispatcher.DispatchAsync(
                CreateContext(),
                request);

        var response =
            Assert.IsType<RespBulkString>(result);

        Assert.Equal("hello", response.Value);
    }

    [Fact]
    public async Task DispatchAsync_IsCaseInsensitive() {
        var request = new RespArray(
        [
            new RespBulkString("ping")
        ]);

        RespValue result =
            await _dispatcher.DispatchAsync(
                CreateContext(),
                request);

        var response =
            Assert.IsType<RespSimpleString>(result);

        Assert.Equal("PONG", response.Value);
    }

    [Fact]
    public async Task DispatchAsync_UnknownCommand_Throws() {
        var request = new RespArray(
        [
            new RespBulkString("UNKNOWN")
        ]);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await _dispatcher.DispatchAsync(
                    CreateContext(),
                    request));
    }

    [Fact]
    public async Task DispatchAsync_EmptyCommand_Throws() {
        var request = new RespArray([]);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await _dispatcher.DispatchAsync(
                    CreateContext(),
                    request));
    }

    [Fact]
    public async Task DispatchAsync_NullArray_Throws() {
        var request = new RespArray(null);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await _dispatcher.DispatchAsync(
                    CreateContext(),
                    request));
    }
}