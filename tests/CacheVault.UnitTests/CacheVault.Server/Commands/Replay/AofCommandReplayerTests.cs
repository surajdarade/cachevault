using CacheVault.Persistence.Aof.Reading;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Commands.Replay;
using CacheVault.Server.Networking.Connections;
using System.Text;

namespace CacheVault.UnitTests.Commands.Replay;

public sealed class AofCommandReplayerTests {
    [Fact]
    public async Task ReplayAsync_ShouldReplayCommandsInOrder() {
        const string aof =
            "*3\r\n" +
            "$3\r\n" +
            "SET\r\n" +
            "$3\r\n" +
            "key\r\n" +
            "$5\r\n" +
            "value\r\n" +
            "*1\r\n" +
            "$3\r\n" +
            "GET\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        var setCommand = new RecordingCommand("SET");
        var getCommand = new RecordingCommand("GET");

        dispatcher.RegisterCommands(
            [setCommand, getCommand]);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        int replayed =
            await replayer.ReplayAsync(stream);

        Assert.Equal(2, replayed);
        Assert.Equal(1, setCommand.ExecutionCount);
        Assert.Equal(1, getCommand.ExecutionCount);
    }

    [Fact]
    public async Task ReplayAsync_ShouldReplayUntilEndOfStream() {
        const string aof =
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n" +
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n" +
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        var command =
            new RecordingCommand("PING");

        dispatcher.RegisterCommands(
            [command]);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        int replayed =
            await replayer.ReplayAsync(
                stream);

        Assert.Equal(
            3,
            replayed);

        Assert.Equal(
            3,
            command.ExecutionCount);
    }

    [Fact]
    public async Task ReplayAsync_ShouldPassCommandArgumentsToDispatcher() {
        const string aof =
            "*3\r\n" +
            "$3\r\n" +
            "SET\r\n" +
            "$3\r\n" +
            "key\r\n" +
            "$5\r\n" +
            "value\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        var command =
            new RecordingCommand("SET");

        dispatcher.RegisterCommands(
            [command]);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        await replayer.ReplayAsync(stream);

        Assert.Single(command.ExecutedArguments);

        Assert.Equal(
            "key",
            command.ExecutedArguments[0][0]);

        Assert.Equal(
             "value",
             command.ExecutedArguments[0][1]);
    }

    [Fact]
    public async Task ReplayAsync_ShouldNotQueueCommandsInTransaction() {
        const string aof =
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        var command =
            new RecordingCommand("PING");

        dispatcher.RegisterCommands(
            [command]);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        await replayer.ReplayAsync(stream);

        Assert.Equal(
            1,
            command.ExecutionCount);
    }

    [Fact]
    public async Task ReplayAsync_ShouldRespectCancellation() {
        const string aof =
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        var command =
            new RecordingCommand("PING");

        dispatcher.RegisterCommands(
            [command]);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await replayer.ReplayAsync(
                    stream,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ReplayAsync_ShouldPropagateUnknownCommandError() {
        const string aof =
            "*1\r\n" +
            "$7\r\n" +
            "UNKNOWN\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            []);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await replayer.ReplayAsync(stream));
    }

    [Fact]
    public async Task ReplayAsync_ShouldReturnZeroForEmptyAof() {
        using var stream =
            new MemoryStream();

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            []);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        int replayed =
            await replayer.ReplayAsync(stream);

        Assert.Equal(
            0,
            replayed);
    }

    [Fact]
    public async Task ReplayAsync_ShouldRejectUnreadableStream() {
        using var stream =
            new NonReadableStream();

        var reader =
            new AofCommandReader(
                new RespParser());

        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            []);

        var replayer =
            new AofCommandReplayer(
                reader,
                dispatcher);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await replayer.ReplayAsync(stream));
    }

    private sealed class RecordingCommand : IRedisCommand {
        public RecordingCommand(string name) {
            Name = name;
        }

        public string Name { get; }

        public int ExecutionCount { get; private set; }

        public List<IReadOnlyList<string>> ExecutedArguments { get; } = [];

        public ValueTask<RespValue> ExecuteAsync(
            CommandContext context,
            IReadOnlyList<RespValue> arguments) {
            ExecutionCount++;

            var values =
                arguments
                    .Select(argument =>
                        argument is RespBulkString bulkString
                            ? bulkString.Value ?? "<null>"
                            : argument.ToString()!)
                    .ToArray();

            ExecutedArguments.Add(values);

            return ValueTask.FromResult<RespValue>(
                new RespSimpleString("OK"));
        }
    }

    private sealed class NonReadableStream : Stream {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() {
            throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }
    }
}