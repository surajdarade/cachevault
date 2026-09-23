using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;


namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class GetCommandTests {
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);
    private readonly InMemoryKeyValueStore _store;
    private readonly CommandContext _context;

    public GetCommandTests() {
        _context = new CommandContext(
            new ClientSession(),
            CancellationToken.None);
        _store =
            new InMemoryKeyValueStore(
            _clock);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingKey_ReturnsValue() {
        _store.Set(
            "name",
            "Suraj");

        var command = new GetCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [new RespBulkString("name")]);

        Assert.Equal(
            new RespBulkString("Suraj"),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_MissingKey_ReturnsNullBulkString() {
        var command = new GetCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [new RespBulkString("missing")]);

        Assert.Equal(
            new RespBulkString(null),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_WrongArgumentCount_Throws() {
        var command = new GetCommand(_store);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () => 
                await command.ExecuteAsync(
                _context,
                []));
    }
}