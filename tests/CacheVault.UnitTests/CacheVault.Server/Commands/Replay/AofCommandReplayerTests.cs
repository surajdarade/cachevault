using System.Text;
using CacheVault.Persistence.Aof.Reading;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Commands.Replay;

namespace CacheVault.UnitTests.Server.Commands.Replay;

public sealed class AofCommandReplayerTests {
    [Fact]
    public async Task ReplayAsync_CompleteAof_ReturnsReplayedCommandCount() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                new RecordingCommand("SET")
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\nkey\r\n" +
                "$5\r\nvalue\r\n");

        using var stream =
            new MemoryStream(data);

        int replayedCommands =
            await replayer.ReplayAsync(
                stream);

        Assert.Equal(
            1,
            replayedCommands);
    }

    [Fact]
    public async Task ReplayAsync_WithStartOffset_ReplaysOnlyCommandsAfterOffset() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        var command =
            new RecordingCommand("SET");

        dispatcher.RegisterCommands(
            [
                command
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        byte[] firstCommand =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\none\r\n" +
                "$3\r\nold\r\n");

        byte[] secondCommand =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\ntwo\r\n" +
                "$3\r\nnew\r\n");

        byte[] data =
            firstCommand
                .Concat(secondCommand)
                .ToArray();

        using var stream =
            new MemoryStream(data);

        int replayedCommands =
            await replayer.ReplayAsync(
                stream,
                firstCommand.Length);

        Assert.Equal(
            1,
            replayedCommands);

        Assert.Equal(
            ["two"],
            command.Keys);
    }

    [Fact]
    public async Task ReplayAsync_WithZeroStartOffset_ReplaysEntireAof() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        var command =
            new RecordingCommand("SET");

        dispatcher.RegisterCommands(
            [
                command
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\none\r\n" +
                "$3\r\nold\r\n" +
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\ntwo\r\n" +
                "$3\r\nnew\r\n");

        using var stream =
            new MemoryStream(data);

        int replayedCommands =
            await replayer.ReplayAsync(
                stream,
                0);

        Assert.Equal(
            2,
            replayedCommands);

        Assert.Equal(
            ["one", "two"],
            command.Keys);
    }

    [Fact]
    public async Task ReplayAsync_WithStartOffsetAtEndOfStream_ReturnsZero() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                new RecordingCommand("SET")
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\nkey\r\n" +
                "$5\r\nvalue\r\n");

        using var stream =
            new MemoryStream(data);

        int replayedCommands =
            await replayer.ReplayAsync(
                stream,
                stream.Length);

        Assert.Equal(
            0,
            replayedCommands);
    }

    [Fact]
    public async Task ReplayAsync_WithNegativeStartOffset_ThrowsArgumentOutOfRangeException() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                new RecordingCommand("SET")
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        using var stream =
            new MemoryStream();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await replayer.ReplayAsync(
                    stream,
                    -1));
    }

    [Fact]
    public async Task ReplayAsync_WithStartOffsetBeyondStreamLength_ThrowsArgumentOutOfRangeException() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                new RecordingCommand("SET")
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        using var stream =
            new MemoryStream(
                [1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await replayer.ReplayAsync(
                    stream,
                    stream.Length + 1));
    }

    [Fact]
    public async Task ReplayAsync_WithNonSeekableStream_ThrowsArgumentException() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                new RecordingCommand("SET")
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        using var stream =
            new NonSeekableReadStream();

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                async () =>
                    await replayer.ReplayAsync(
                        stream,
                        0));

        Assert.Contains(
            "seeking",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplayAsync_TruncatedAof_ThrowsInvalidDataException() {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                new RecordingCommand("SET")
            ]);

        dispatcher.RegisterPersistence(
            new RecordingPersistence());

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\nSET\r\n" +
                "$3\r\nkey\r\n" +
                "$5\r\nval");

        using var stream =
            new MemoryStream(data);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await replayer.ReplayAsync(
                    stream));
    }

    private sealed class RecordingPersistence :
        ICommandPersistence {
        public ValueTask PersistAsync(
            string commandName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCommand :
        IRedisCommand {
        public RecordingCommand(
            string name) {
            Name = name;
        }

        public string Name { get; }

        public List<string> Keys { get; } = [];

        public ValueTask<RespValue> ExecuteAsync(
            CommandContext context,
            IReadOnlyList<RespValue> arguments) {
            if (arguments.Count > 0 &&
                arguments[0] is RespBulkString key &&
                key.Value is not null) {
                Keys.Add(
                    key.Value);
            }

            return ValueTask.FromResult<RespValue>(
                new RespSimpleString("OK"));
        }
    }

    private sealed class NonSeekableReadStream :
        MemoryStream {
        public override bool CanSeek =>
            false;

        public override long Seek(
            long offset,
            SeekOrigin loc) {
            throw new NotSupportedException();
        }

        public override long Position {
            get => base.Position;
            set => throw new NotSupportedException();
        }
    }
}