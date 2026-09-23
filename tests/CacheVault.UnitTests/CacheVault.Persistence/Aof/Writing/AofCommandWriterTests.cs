using System.Text;
using CacheVault.Persistence.Aof.Writing;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Protocol.Resp.Serialization;

namespace CacheVault.UnitTests.CacheVault.Persistence.Aof.Writing;

public sealed class AofCommandWriterTests {
    [Fact]
    public void WriteCommand_WithCommandOnly_WritesRespArray() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "PING",
            []);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*1\r\n$4\r\nPING\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_WithSingleArgument_WritesCommandAndArgument() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "GET",
            ["mykey"]);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*2\r\n$3\r\nGET\r\n$5\r\nmykey\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_WithMultipleArguments_WritesAllArguments() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "SET",
            [
                "name",
                "CacheVault"
            ]);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*3\r\n" +
            "$3\r\nSET\r\n" +
            "$4\r\nname\r\n" +
            "$10\r\nCacheVault\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_WithTtlArguments_WritesAllArguments() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "SET",
            [
                "session",
                "active",
                "PX",
                "30000"
            ]);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*5\r\n" +
            "$3\r\nSET\r\n" +
            "$7\r\nsession\r\n" +
            "$6\r\nactive\r\n" +
            "$2\r\nPX\r\n" +
            "$5\r\n30000\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_WithUnicodeArguments_WritesUtf8Correctly() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "SET",
            [
                "नमस्ते",
                "CacheVault 🚀"
            ]);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*3\r\n" +
            "$3\r\nSET\r\n" +
            "$18\r\nनमस्ते\r\n" +
            "$15\r\nCacheVault 🚀\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_WithEmptyArgument_WritesEmptyBulkString() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "SET",
            [
                "key",
                string.Empty
            ]);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*3\r\n" +
            "$3\r\nSET\r\n" +
            "$3\r\nkey\r\n" +
            "$0\r\n\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_AppendsMultipleCommandsToSameStream() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "SET",
            [
                "key",
                "value"
            ]);

        writer.WriteCommand(
            stream,
            "GET",
            [
                "key"
            ]);

        writer.WriteCommand(
            stream,
            "DEL",
            [
                "key"
            ]);

        string result =
            Encoding.UTF8.GetString(
                stream.ToArray());

        Assert.Equal(
            "*3\r\n" +
            "$3\r\nSET\r\n" +
            "$3\r\nkey\r\n" +
            "$5\r\nvalue\r\n" +
            "*2\r\n" +
            "$3\r\nGET\r\n" +
            "$3\r\nkey\r\n" +
            "*2\r\n" +
            "$3\r\nDEL\r\n" +
            "$3\r\nkey\r\n",
            result);
    }

    [Fact]
    public void WriteCommand_WritesValidRespThatCanBeParsed() {
        var serializer =
            new RespSerializer();

        var writer =
            new AofCommandWriter(
                serializer);

        using var stream =
            new MemoryStream();

        writer.WriteCommand(
            stream,
            "SET",
            [
                "name",
                "Suraj"
            ]);

        stream.Position = 0;

        var parser =
            new RespParser();

        RespValue value =
            parser.Parse(
                stream.ToArray());

        var array =
            Assert.IsType<RespArray>(
                value);

        Assert.NotNull(
            array.Values);

        Assert.Equal(
            3,
            array.Values.Count);

        Assert.Equal(
            "SET",
            Assert.IsType<RespBulkString>(
                array.Values[0]).Value);

        Assert.Equal(
            "name",
            Assert.IsType<RespBulkString>(
                array.Values[1]).Value);

        Assert.Equal(
            "Suraj",
            Assert.IsType<RespBulkString>(
                array.Values[2]).Value);
    }

    [Fact]
    public void WriteCommand_WithNullSerializer_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () =>
                new AofCommandWriter(
                    null!));
    }

    [Fact]
    public void WriteCommand_WithNullStream_ThrowsArgumentNullException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        Assert.Throws<ArgumentNullException>(
            () =>
                writer.WriteCommand(
                    null!,
                    "PING",
                    []));
    }

    [Fact]
    public void WriteCommand_WithNullCommandName_ThrowsArgumentNullException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentNullException>(
            () =>
                writer.WriteCommand(
                    stream,
                    null!,
                    []));
    }

    [Fact]
    public void WriteCommand_WithEmptyCommandName_ThrowsArgumentException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentException>(
            () =>
                writer.WriteCommand(
                    stream,
                    string.Empty,
                    []));
    }

    [Fact]
    public void WriteCommand_WithWhitespaceCommandName_ThrowsArgumentException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentException>(
            () =>
                writer.WriteCommand(
                    stream,
                    "   ",
                    []));
    }

    [Fact]
    public void WriteCommand_WithNullArguments_ThrowsArgumentNullException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentNullException>(
            () =>
                writer.WriteCommand(
                    stream,
                    "PING",
                    null!));
    }

    [Fact]
    public void WriteCommand_WithNullArgument_ThrowsArgumentNullException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentNullException>(
            () =>
                writer.WriteCommand(
                    stream,
                    "SET",
                    [
                        "key",
                        null!
                    ]));
    }

    [Fact]
    public void WriteCommand_WithReadOnlyStream_ThrowsArgumentException() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new ReadOnlyTestStream();

        Assert.Throws<ArgumentException>(
            () =>
                writer.WriteCommand(
                    stream,
                    "PING",
                    []));
    }

    [Fact]
    public void WriteCommand_DoesNotWriteAnythingWhenCommandNameIsInvalid() {
        var writer =
            new AofCommandWriter(
                new RespSerializer());

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentException>(
            () =>
                writer.WriteCommand(
                    stream,
                    string.Empty,
                    []));

        Assert.Empty(
            stream.ToArray());
    }

    private sealed class ReadOnlyTestStream :
        MemoryStream {
        public override bool CanWrite =>
            false;

        public override void Write(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }

        public override void Write(
            ReadOnlySpan<byte> buffer) {
            throw new NotSupportedException();
        }

        public override void WriteByte(
            byte value) {
            throw new NotSupportedException();
        }
    }
}