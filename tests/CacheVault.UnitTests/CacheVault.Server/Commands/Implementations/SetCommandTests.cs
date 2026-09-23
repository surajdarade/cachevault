using CacheVault.Core.Storage;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.Networking.Connections;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Implementations;

public sealed class SetCommandTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly InMemoryKeyValueStore _store;
    private readonly CommandContext _context;

    public SetCommandTests() {
        _context =
            new CommandContext(
                new ClientSession(),
                CancellationToken.None);

        _store =
            new InMemoryKeyValueStore(
                _clock);
    }

    [Fact]
    public async Task ExecuteAsync_ValidArguments_StoresValue() {
        var command =
            new SetCommand(
                _store,
                _clock);

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

        Assert.Null(
            value.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WrongArgumentCount_Throws() {
        var command =
            new SetCommand(
                _store,
                _clock);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await command.ExecuteAsync(
                    _context,
                    [
                        new RespBulkString("key")
                    ]));
    }

    [Fact]
    public async Task ExecuteAsync_Ex_SetsExpiration() {
        var command =
            new SetCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key"),
                    new RespBulkString("value"),
                    new RespBulkString("EX"),
                    new RespBulkString("10")
                ]);

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.True(
            _store.TryGet(
                "key",
                out var storedValue));

        Assert.NotNull(storedValue);

        Assert.Equal(
            "value",
            storedValue.Value);

        Assert.Equal(
            _clock.UtcNow.AddSeconds(10),
            storedValue.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_Px_SetsExpiration() {
        var command =
            new SetCommand(
                _store,
                _clock);

        RespValue result =
            await command.ExecuteAsync(
                _context,
                [
                    new RespBulkString("key"),
                    new RespBulkString("value"),
                    new RespBulkString("PX"),
                    new RespBulkString("5000")
                ]);

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.True(
            _store.TryGet(
                "key",
                out var storedValue));

        Assert.NotNull(storedValue);

        Assert.Equal(
            "value",
            storedValue.Value);

        Assert.Equal(
            _clock.UtcNow.AddMilliseconds(5000),
            storedValue.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_Ex_IsCaseInsensitive() {
        var command =
            new SetCommand(
                _store,
                _clock);

        await command.ExecuteAsync(
            _context,
            [
                new RespBulkString("key"),
                new RespBulkString("value"),
                new RespBulkString("ex"),
                new RespBulkString("10")
            ]);

        Assert.True(
            _store.TryGet(
                "key",
                out var storedValue));

        Assert.NotNull(storedValue);

        Assert.Equal(
            _clock.UtcNow.AddSeconds(10),
            storedValue.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_Px_IsCaseInsensitive() {
        var command =
            new SetCommand(
                _store,
                _clock);

        await command.ExecuteAsync(
            _context,
            [
                new RespBulkString("key"),
                new RespBulkString("value"),
                new RespBulkString("px"),
                new RespBulkString("5000")
            ]);

        Assert.True(
            _store.TryGet(
                "key",
                out var storedValue));

        Assert.NotNull(storedValue);

        Assert.Equal(
            _clock.UtcNow.AddMilliseconds(5000),
            storedValue.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_ExZero_Throws() {
        var command =
            new SetCommand(
                _store,
                _clock);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("key"),
                            new RespBulkString("value"),
                            new RespBulkString("EX"),
                            new RespBulkString("0")
                        ]));

        Assert.Equal(
            "ERR invalid expire time in 'set' command",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PxZero_Throws() {
        var command =
            new SetCommand(
                _store,
                _clock);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("key"),
                            new RespBulkString("value"),
                            new RespBulkString("PX"),
                            new RespBulkString("0")
                        ]));

        Assert.Equal(
            "ERR invalid expire time in 'set' command",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeExpiration_Throws() {
        var command =
            new SetCommand(
                _store,
                _clock);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("key"),
                            new RespBulkString("value"),
                            new RespBulkString("EX"),
                            new RespBulkString("-10")
                        ]));

        Assert.Equal(
            "ERR invalid expire time in 'set' command",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NonNumericExpiration_Throws() {
        var command =
            new SetCommand(
                _store,
                _clock);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("key"),
                            new RespBulkString("value"),
                            new RespBulkString("EX"),
                            new RespBulkString("abc")
                        ]));

        Assert.Equal(
            "ERR invalid expire time in 'set' command",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownExpirationOption_Throws() {
        var command =
            new SetCommand(
                _store,
                _clock);

        var exception =
            await Assert.ThrowsAsync<CommandArgumentException>(
                async () =>
                    await command.ExecuteAsync(
                        _context,
                        [
                            new RespBulkString("key"),
                            new RespBulkString("value"),
                            new RespBulkString("INVALID"),
                            new RespBulkString("10")
                        ]));

        Assert.Equal(
            "ERR syntax error",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Ex_ValueExpiresAfterSpecifiedDuration() {
        var command =
            new SetCommand(
                _store,
                _clock);

        await command.ExecuteAsync(
            _context,
            [
                new RespBulkString("key"),
                new RespBulkString("value"),
                new RespBulkString("EX"),
                new RespBulkString("10")
            ]);

        _clock.Advance(
            TimeSpan.FromSeconds(9));

        Assert.True(
            _store.TryGet(
                "key",
                out _));

        _clock.Advance(
            TimeSpan.FromSeconds(1));

        Assert.False(
            _store.TryGet(
                "key",
                out _));
    }

    [Fact]
    public async Task ExecuteAsync_Px_ValueExpiresAfterSpecifiedDuration() {
        var command =
            new SetCommand(
                _store,
                _clock);

        await command.ExecuteAsync(
            _context,
            [
                new RespBulkString("key"),
                new RespBulkString("value"),
                new RespBulkString("PX"),
                new RespBulkString("5000")
            ]);

        _clock.Advance(
            TimeSpan.FromMilliseconds(4999));

        Assert.True(
            _store.TryGet(
                "key",
                out _));

        _clock.Advance(
            TimeSpan.FromMilliseconds(1));

        Assert.False(
            _store.TryGet(
                "key",
                out _));
    }
}