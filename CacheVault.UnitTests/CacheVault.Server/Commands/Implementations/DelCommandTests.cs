using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class DelCommandTests {
    private readonly InMemoryKeyValueStore _store = new();
    private readonly CommandContext _context;

    public DelCommandTests() {
        _context = new CommandContext(
            new ClientSession(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingKey_ReturnsOne() {
        _store.Set(
            "name",
            "Suraj");

        var command = new DelCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [new RespBulkString("name")]);

        Assert.Equal(
            new RespInteger(1),
            result);

        Assert.False(
            _store.Contains("name"));
    }

    [Fact]
    public async Task ExecuteAsync_MissingKey_ReturnsZero() {
        var command = new DelCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [new RespBulkString("missing")]);

        Assert.Equal(
            new RespInteger(0),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleKeys_ReturnsDeletedCount() {
        _store.Set("key1", "value1");
        _store.Set("key2", "value2");

        var command = new DelCommand(_store);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key1"),
                    new RespBulkString("key2"),
                    new RespBulkString("key3")
                ]);

        Assert.Equal(
            new RespInteger(2),
            result);
    }

    [Fact]
    public async Task ExecuteAsync_NoArguments_Throws() {
        var command = new DelCommand(_store);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await command.ExecuteAsync(
                _context,
                []));
    }
}