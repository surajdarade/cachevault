using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class IncrCommandTests {
    private readonly InMemoryKeyValueStore _store = new();
    private readonly CommandContext _context;

    public IncrCommandTests() {
        _context = new CommandContext(
            new ClientSession(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_MissingKey_CreatesOne() {
        var command = new IncrCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [new RespBulkString("counter")]);

        Assert.Equal(
            new RespInteger(1),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingInteger_IncrementsValue() {
        _store.Set(
            "counter",
            "10");

        var command = new IncrCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [new RespBulkString("counter")]);

        Assert.Equal(
            new RespInteger(11),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_NonIntegerValue_ReturnsError() {
        _store.Set(
            "counter",
            "hello");

        var command = new IncrCommand(_store);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                await command.ExecuteAsync(
                    _context,
                    [new RespBulkString("counter")]));

        Assert.Equal(
            "ERR value is not an integer or out of range",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WrongArgumentCount_Throws() {
        var command = new IncrCommand(_store);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await command.ExecuteAsync(
                _context,
                []));
    }
}
