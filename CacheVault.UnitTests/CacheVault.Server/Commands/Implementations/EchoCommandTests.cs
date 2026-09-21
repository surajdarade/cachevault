using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class EchoCommandTests {
    private readonly EchoCommand _command = new();

    private static CommandContext CreateContext() {
        return new CommandContext(
            new ClientSession(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithMessage_ReturnsMessage() {
        RespValue result = await _command.ExecuteAsync(
            CreateContext(),
            [
                new RespBulkString("hello")
            ]);

        var response =
            Assert.IsType<RespBulkString>(result);

        Assert.Equal("hello", response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutArguments_Throws() {
        await Assert.ThrowsAsync<CommandArgumentException>(
            async () => await _command.ExecuteAsync(
                CreateContext(),
                []));
    }

    [Fact]
    public async Task ExecuteAsync_WithTooManyArguments_Throws() {
        await Assert.ThrowsAsync<CommandArgumentException>(
            async () => await _command.ExecuteAsync(
                CreateContext(),
                [
                    new RespBulkString("one"),
                    new RespBulkString("two")
                ]));
    }
}