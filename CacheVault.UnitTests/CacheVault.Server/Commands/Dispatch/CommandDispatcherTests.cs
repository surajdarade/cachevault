using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Dispatch;

public sealed class CommandDispatcherTests {
    private readonly CommandDispatcher _dispatcher =
        new(CommandRegistry.CreateDefaultCommands(new InMemoryKeyValueStore()));

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
            async () => await _dispatcher.DispatchAsync(
                CreateContext(),
                request));
    }

    [Fact]
    public async Task DispatchAsync_EmptyCommand_Throws() {
        var request = new RespArray([]);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () => await _dispatcher.DispatchAsync(
                CreateContext(),
                request));
    }

    [Fact]
    public async Task DispatchAsync_NullArray_Throws() {
        var request = new RespArray(null);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () => await _dispatcher.DispatchAsync(
                CreateContext(),
                request));
    }
}