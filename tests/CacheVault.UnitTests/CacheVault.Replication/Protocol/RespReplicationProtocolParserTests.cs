using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Protocol;

namespace CacheVault.UnitTests.CacheVault.Replication.Protocol;

public sealed class RespReplicationProtocolParserTests {
    private readonly RespReplicationProtocolParser _parser = new();

    [Fact]
    public void ParseReplConf_ShouldParseListeningPort() {
        var value =
            new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("listening-port"),
                new RespBulkString("6380")
            ]);

        ReplConfCommand result =
            _parser.ParseReplConf(value);

        Assert.Equal(
            "listening-port",
            result.SubCommand);

        Assert.Equal(
            ["6380"],
            result.Arguments);
    }

    [Fact]
    public void ParseReplConf_ShouldParseCapability() {
        var value =
            new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("capa"),
                new RespBulkString("psync2")
            ]);

        ReplConfCommand result =
            _parser.ParseReplConf(value);

        Assert.Equal(
            "capa",
            result.SubCommand);

        Assert.Equal(
            ["psync2"],
            result.Arguments);
    }

    [Fact]
    public void ParsePsync_ShouldParseInitialSynchronization() {
        var value =
            new RespArray(
            [
                new RespBulkString("PSYNC"),
                new RespBulkString("?"),
                new RespBulkString("-1")
            ]);

        PsyncCommand result =
            _parser.ParsePsync(value);

        Assert.Equal(
            "?",
            result.ReplicationId);

        Assert.Equal(
            -1,
            result.Offset);
    }

    [Fact]
    public void ParsePsync_ShouldParseKnownOffset() {
        var value =
            new RespArray(
            [
                new RespBulkString(
                    "PSYNC"),
                new RespBulkString(
                    "0123456789abcdef"),
                new RespBulkString(
                    "500")
            ]);

        PsyncCommand result =
            _parser.ParsePsync(value);

        Assert.Equal(
            "0123456789abcdef",
            result.ReplicationId);

        Assert.Equal(
            500,
            result.Offset);
    }

    [Fact]
    public void ParseFullResync_ShouldParseResponse() {
        var value =
            new RespSimpleString(
                "FULLRESYNC replication-id 1000");

        FullResyncResponse result =
            _parser.ParseFullResync(value);

        Assert.Equal(
            "replication-id",
            result.ReplicationId);

        Assert.Equal(
            1000,
            result.ReplicationOffset);
    }

    [Fact]
    public void IsContinue_ShouldReturnTrue() {
        var value =
            new RespSimpleString(
                "CONTINUE");

        Assert.True(
            _parser.IsContinue(value));
    }

    [Fact]
    public void IsContinue_ShouldBeCaseInsensitive() {
        var value =
            new RespSimpleString(
                "continue");

        Assert.True(
            _parser.IsContinue(value));
    }

    [Fact]
    public void IsContinue_ShouldReturnFalseForOtherResponse() {
        var value =
            new RespSimpleString(
                "FULLRESYNC id 100");

        Assert.False(
            _parser.IsContinue(value));
    }

    [Fact]
    public void ParseAck_ShouldParseOffset() {
        var value =
            new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("ACK"),
                new RespBulkString("12345")
            ]);

        ReplicationAck result =
            _parser.ParseAck(value);

        Assert.Equal(
            12345,
            result.Offset);
    }

    [Fact]
    public void ParseAck_ShouldBeCaseInsensitive() {
        var value =
            new RespArray(
            [
                new RespBulkString("replconf"),
                new RespBulkString("ack"),
                new RespBulkString("500")
            ]);

        ReplicationAck result =
            _parser.ParseAck(value);

        Assert.Equal(
            500,
            result.Offset);
    }

    [Fact]
    public void ParsePsync_WithInvalidOffset_ShouldThrow() {
        var value =
            new RespArray(
            [
                new RespBulkString("PSYNC"),
                new RespBulkString("?"),
                new RespBulkString("invalid")
            ]);

        Assert.Throws<FormatException>(
            () =>
                _parser.ParsePsync(value));
    }

    [Fact]
    public void ParseAck_WithInvalidOffset_ShouldThrow() {
        var value =
            new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("ACK"),
                new RespBulkString("invalid")
            ]);

        Assert.Throws<FormatException>(
            () =>
                _parser.ParseAck(value));
    }

    [Fact]
    public void ParsePsync_WithWrongCommand_ShouldThrow() {
        var value =
            new RespArray(
            [
                new RespBulkString("SET"),
                new RespBulkString("key"),
                new RespBulkString("value")
            ]);

        Assert.Throws<FormatException>(
            () =>
                _parser.ParsePsync(value));
    }

    [Fact]
    public void ParseReplConf_WithWrongRespType_ShouldThrow() {
        var value =
            new RespSimpleString(
                "REPLCONF capa psync2");

        Assert.Throws<FormatException>(
            () =>
                _parser.ParseReplConf(value));
    }

    [Fact]
    public void ParseFullResync_WithWrongPrefix_ShouldThrow() {
        var value =
            new RespSimpleString(
                "INVALID replication-id 100");

        Assert.Throws<FormatException>(
            () =>
                _parser.ParseFullResync(value));
    }
}