using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class DiscardCommandTests {
    private readonly CommandContext _context;

    public DiscardCommandTests() {
        _context =
            new CommandContext(
                new ClientSession(),
                CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_DiscardsActiveTransaction() {
        _context.Session.BeginTransaction();

        _context.Session.QueueCommand(
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]));

        var command =
            new DiscardCommand();

        RespValue result =
            await command.ExecuteAsync(
                _context,
                []);

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.False(
            _context.Session.IsInTransaction);

        Assert.Empty(
            _context.Session.QueuedCommands);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTransaction_Throws() {
        var command =
            new DiscardCommand();

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        []));

        Assert.Equal(
            "ERR DISCARD without MULTI",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithArguments_Throws() {
        _context.Session.BeginTransaction();

        var command =
            new DiscardCommand();

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("unexpected")
                        ]));

        Assert.Equal(
            "ERR wrong number of arguments for 'discard' command",
            exception.Message);

        Assert.True(
            _context.Session.IsInTransaction);
    }
}