using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class SetCommandTests {
    private readonly InMemoryKeyValueStore _store = new();
    private readonly CommandContext _context;

    public SetCommandTests() {
        _context = new CommandContext(
            new ClientSession(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ValidArguments_StoresValue() {
        var command = new SetCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("name"),
                    new RespBulkString("Suraj")
                ]);

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.True(
            _store.TryGet(
                "name",
                out var value));

        Assert.NotNull(value);
        Assert.Equal(
            "Suraj",
            value.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WrongArgumentCount_Throws() {
        var command = new SetCommand(_store);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await command.ExecuteAsync(
                _context,
                [new RespBulkString("key")]));
    }
}
