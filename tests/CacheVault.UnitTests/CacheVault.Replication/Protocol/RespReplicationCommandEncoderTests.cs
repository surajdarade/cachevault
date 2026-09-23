using System.Text;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Protocol;

public sealed class RespReplicationCommandEncoderTests {
    [Fact]
    public void Encode_SetCommand_ShouldProduceRespCommand() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                0,
                "SET",
                ["name", "Suraj"]);

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\n" +
                "SET\r\n" +
                "$4\r\n" +
                "name\r\n" +
                "$5\r\n" +
                "Suraj\r\n");

        Assert.Equal(
            expected,
            entry.Data);

        Assert.Equal(
            0,
            entry.StartOffset);

        Assert.Equal(
            expected.Length,
            entry.EndOffset);

        Assert.Equal(
            expected.Length,
            entry.Length);
    }

    [Fact]
    public void Encode_DelCommand_ShouldProduceRespCommand() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                100,
                "DEL",
                ["name"]);

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "*2\r\n" +
                "$3\r\n" +
                "DEL\r\n" +
                "$4\r\n" +
                "name\r\n");

        Assert.Equal(
            expected,
            entry.Data);

        Assert.Equal(
            100,
            entry.StartOffset);

        Assert.Equal(
            100 + expected.Length,
            entry.EndOffset);
    }

    [Fact]
    public void Encode_IncrCommand_ShouldProduceRespCommand() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                50,
                "INCR",
                ["counter"]);

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "*2\r\n" +
                "$4\r\n" +
                "INCR\r\n" +
                "$7\r\n" +
                "counter\r\n");

        Assert.Equal(
            expected,
            entry.Data);

        Assert.Equal(
            50,
            entry.StartOffset);

        Assert.Equal(
            50 + expected.Length,
            entry.EndOffset);
    }

    [Fact]
    public void Encode_WithNoArguments_ShouldProduceSingleElementArray() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                0,
                "PING",
                []);

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "*1\r\n" +
                "$4\r\n" +
                "PING\r\n");

        Assert.Equal(
            expected,
            entry.Data);
    }

    [Fact]
    public void Encode_ShouldPreserveArgumentOrder() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                0,
                "COMMAND",
                ["one", "two", "three"]);

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "*4\r\n" +
                "$7\r\n" +
                "COMMAND\r\n" +
                "$3\r\n" +
                "one\r\n" +
                "$3\r\n" +
                "two\r\n" +
                "$5\r\n" +
                "three\r\n");

        Assert.Equal(
            expected,
            entry.Data);
    }

    [Fact]
    public void Encode_WithUnicodeArguments_ShouldUseUtf8() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                0,
                "SET",
                ["greeting", "नमस्ते"]);

        byte[] expected =
            Encoding.UTF8.GetBytes(
                "*3\r\n" +
                "$3\r\n" +
                "SET\r\n" +
                "$8\r\n" +
                "greeting\r\n" +
                "$18\r\n" +
                "नमस्ते\r\n");

        Assert.Equal(
            expected,
            entry.Data);
    }

    [Fact]
    public void Encode_ShouldCalculateOffsetFromByteLength() {
        var encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                1000,
                "SET",
                ["key", "value"]);

        Assert.Equal(
            entry.StartOffset + entry.Data.Length,
            entry.EndOffset);

        Assert.Equal(
            entry.Data.Length,
            entry.Length);
    }

    [Fact]
    public void Encode_WithNegativeStartOffset_ShouldThrow() {
        var encoder =
            CreateEncoder();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                encoder.Encode(
                    -1,
                    "SET",
                    ["key", "value"]));
    }

    [Fact]
    public void Encode_WithNullCommandName_ShouldThrow() {
        var encoder =
            CreateEncoder();

        Assert.Throws<ArgumentNullException>(
            () =>
                encoder.Encode(
                    0,
                    null!,
                    ["key", "value"]));
    }

    [Fact]
    public void Encode_WithEmptyCommandName_ShouldThrow() {
        var encoder =
            CreateEncoder();

        Assert.Throws<ArgumentException>(
            () =>
                encoder.Encode(
                    0,
                    string.Empty,
                    ["key", "value"]));
    }

    [Fact]
    public void Encode_WithNullArguments_ShouldThrow() {
        var encoder =
            CreateEncoder();

        Assert.Throws<ArgumentNullException>(
            () =>
                encoder.Encode(
                    0,
                    "SET",
                    null!));
    }

    [Fact]
    public void Encode_WithNullArgument_ShouldThrow() {
        var encoder =
            CreateEncoder();

        Assert.Throws<ArgumentNullException>(
            () =>
                encoder.Encode(
                    0,
                    "SET",
                    ["key", null!]));
    }

    private static RespReplicationCommandEncoder CreateEncoder() {
        return new RespReplicationCommandEncoder(
            new RespSerializer());
    }
}