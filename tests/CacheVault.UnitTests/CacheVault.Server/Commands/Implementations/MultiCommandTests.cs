using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class MultiCommandTests {
    private readonly CommandContext _context;

    public MultiCommandTests() {
        _context =
            new CommandContext(
                new ClientSession(),
                CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_StartsTransaction() {
        var command =
            new MultiCommand();

        RespValue result =
            await command.ExecuteAsync(
                _context,
                []);

        var response =
            Assert.IsType<RespSimpleString>(
                result);

        Assert.Equal(
            "OK",
            response.Value);

        Assert.True(
            _context.Session.IsInTransaction);

        Assert.Empty(
            _context.Session.QueuedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyInTransaction_Throws() {
        _context.Session.BeginTransaction();

        var command =
            new MultiCommand();

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        []));

        Assert.Equal(
            "ERR MULTI calls can not be nested",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithArguments_Throws() {
        var command =
            new MultiCommand();

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("unexpected")
                        ]));

        Assert.Equal(
            "ERR wrong number of arguments for 'multi' command",
            exception.Message);

        Assert.False(
            _context.Session.IsInTransaction);
    }
}