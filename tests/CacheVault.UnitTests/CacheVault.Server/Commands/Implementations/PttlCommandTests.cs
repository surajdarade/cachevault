using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class PttlCommandTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly InMemoryKeyValueStore _store;
    private readonly CommandContext _context;

    public PttlCommandTests() {
        _store =
            new InMemoryKeyValueStore(
                _clock);

        _context =
            new CommandContext(
                new ClientSession(),
                CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_MissingKey_ReturnsMinusTwo() {
        var command =
            new PttlCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("missing")
                ]);

        var response =
            Assert.IsType<RespInteger>(result);

        Assert.Equal(
            -2,
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_PersistentKey_ReturnsMinusOne() {
        _store.Set(
            "key",
            "value");

        var command =
            new PttlCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key")
                ]);

        var response =
            Assert.IsType<RespInteger>(result);

        Assert.Equal(
            -1,
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiringKey_ReturnsRemainingMilliseconds() {
        _store.Set(
            "key",
            "value",
            _clock.UtcNow.AddMilliseconds(5000));

        var command =
            new PttlCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key")
                ]);

        var response =
            Assert.IsType<RespInteger>(result);

        Assert.Equal(
            5000,
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_AfterTimeAdvances_ReturnsRemainingMilliseconds() {
        _store.Set(
            "key",
            "value",
            _clock.UtcNow.AddMilliseconds(5000));

        _clock.Advance(
            TimeSpan.FromMilliseconds(1250));

        var command =
            new PttlCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key")
                ]);

        var response =
            Assert.IsType<RespInteger>(result);

        Assert.Equal(
            3750,
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_SubMillisecondRemainingTime_FloorsResult() {
        _store.Set(
            "key",
            "value",
            _clock.UtcNow.AddMilliseconds(5000));

        _clock.Advance(
            TimeSpan.FromMilliseconds(1250));

        _clock.Advance(
            TimeSpan.FromTicks(1));

        var command =
            new PttlCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key")
                ]);

        var response =
            Assert.IsType<RespInteger>(result);

        Assert.Equal(
            3749,
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_AtExpiration_ReturnsMinusTwo() {
        _store.Set(
            "key",
            "value",
            _clock.UtcNow.AddMilliseconds(5000));

        _clock.Advance(
            TimeSpan.FromMilliseconds(5000));

        var command =
            new PttlCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key")
                ]);

        var response =
            Assert.IsType<RespInteger>(result);

        Assert.Equal(
            -2,
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WrongArgumentCount_Throws() {
        var command =
            new PttlCommand(
                _store,
                _clock);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        []));

        Assert.Equal(
            "ERR wrong number of arguments for 'pttl' command",
            exception.Message);
    }
}