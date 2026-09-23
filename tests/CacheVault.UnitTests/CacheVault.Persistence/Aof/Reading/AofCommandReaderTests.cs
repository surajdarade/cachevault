using System.Text;
using CacheVault.Persistence.Aof.Reading;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.UnitTests.Persistence.Aof.Reading;

public sealed class AofCommandReaderTests {
    [Fact]
    public void ReadCommand_ShouldReadPingCommand() {
        const string aof =
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        RespArray command =
            reader.ReadCommand(stream);

        Assert.NotNull(command.Values);
        Assert.Single(command.Values);

        var commandName =
            Assert.IsType<RespBulkString>(
                command.Values[0]);

        Assert.Equal(
            "PING",
            commandName.Value);
    }

    [Fact]
    public void ReadCommand_ShouldReadCommandWithArguments() {
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

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        RespArray command =
            reader.ReadCommand(stream);

        Assert.NotNull(command.Values);
        Assert.Equal(3, command.Values.Count);

        var commandName =
            Assert.IsType<RespBulkString>(
                command.Values[0]);

        var key =
            Assert.IsType<RespBulkString>(
                command.Values[1]);

        var value =
            Assert.IsType<RespBulkString>(
                command.Values[2]);

        Assert.Equal("SET", commandName.Value);
        Assert.Equal("key", key.Value);
        Assert.Equal("value", value.Value);
    }

    [Fact]
    public void ReadCommand_ShouldHandleFragmentedStreamReads() {
        const string aof =
            "*2\r\n" +
            "$4\r\n" +
            "ECHO\r\n" +
            "$5\r\n" +
            "hello\r\n";

        byte[] data =
            Encoding.UTF8.GetBytes(aof);

        using var stream =
            new FragmentedReadStream(
                data,
                maxBytesPerRead: 2);

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        RespArray command =
            reader.ReadCommand(stream);

        Assert.NotNull(command.Values);
        Assert.Equal(2, command.Values.Count);

        var commandName =
            Assert.IsType<RespBulkString>(
                command.Values[0]);

        var argument =
            Assert.IsType<RespBulkString>(
                command.Values[1]);

        Assert.Equal("ECHO", commandName.Value);
        Assert.Equal("hello", argument.Value);
    }

    [Fact]
    public void ReadCommand_ShouldReadMultipleCommandsFromSameStream() {
        const string aof =
            "*1\r\n" +
            "$4\r\n" +
            "PING\r\n" +
            "*2\r\n" +
            "$4\r\n" +
            "ECHO\r\n" +
            "$5\r\n" +
            "hello\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        RespArray firstCommand =
            reader.ReadCommand(stream);

        RespArray secondCommand =
            reader.ReadCommand(stream);

        Assert.NotNull(firstCommand.Values);
        Assert.Single(firstCommand.Values);

        var firstName =
            Assert.IsType<RespBulkString>(
                firstCommand.Values[0]);

        Assert.Equal(
            "PING",
            firstName.Value);

        Assert.NotNull(secondCommand.Values);
        Assert.Equal(2, secondCommand.Values.Count);

        var secondName =
            Assert.IsType<RespBulkString>(
                secondCommand.Values[0]);

        var secondArgument =
            Assert.IsType<RespBulkString>(
                secondCommand.Values[1]);

        Assert.Equal(
            "ECHO",
            secondName.Value);

        Assert.Equal(
            "hello",
            secondArgument.Value);
    }

    [Fact]
    public void ReadCommand_ShouldRejectEmptyArray() {
        const string aof =
            "*0\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        Assert.Throws<FormatException>(
            () => reader.ReadCommand(stream));
    }

    [Fact]
    public void ReadCommand_ShouldRejectNonArrayRespValue() {
        const string aof =
            "+OK\r\n";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        Assert.Throws<FormatException>(
            () => reader.ReadCommand(stream));
    }

    [Fact]
    public void ReadCommand_ShouldThrowWhenAofCommandIsTruncated() {
        const string aof =
            "*2\r\n" +
            "$4\r\n" +
            "ECHO\r\n" +
            "$5\r\n" +
            "hel";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(aof));

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        Assert.Throws<EndOfStreamException>(
            () => reader.ReadCommand(stream));
    }

    [Fact]
    public void ReadCommand_ShouldThrowWhenStreamIsNotReadable() {
        using var stream =
            new NonReadableStream();

        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        Assert.Throws<ArgumentException>(
            () => reader.ReadCommand(stream));
    }

    [Fact]
    public void Constructor_ShouldRejectNullParser() {
        Assert.Throws<ArgumentNullException>(
            () => new AofCommandReader(null!));
    }

    [Fact]
    public void ReadCommand_ShouldRejectNullStream() {
        var parser = new RespParser();
        var reader = new AofCommandReader(parser);

        Assert.Throws<ArgumentNullException>(
            () => reader.ReadCommand(null!));
    }

    private sealed class FragmentedReadStream : MemoryStream {
        private readonly int _maxBytesPerRead;

        public FragmentedReadStream(
            byte[] buffer,
            int maxBytesPerRead)
            : base(buffer) {
            _maxBytesPerRead =
                maxBytesPerRead;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) {
            return base.Read(
                buffer,
                offset,
                Math.Min(
                    count,
                    _maxBytesPerRead));
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