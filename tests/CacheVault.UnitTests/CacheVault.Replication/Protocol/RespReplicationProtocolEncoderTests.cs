using System.Text;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Replication.Protocol;

namespace CacheVault.UnitTests.CacheVault.Replication.Protocol;

public sealed class RespReplicationProtocolEncoderTests {
    private readonly RespSerializer _serializer = new();

    [Fact]
    public void EncodeReplConf_ShouldEncodeListeningPort() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        var command =
            new ReplConfCommand(
                "listening-port",
                ["6380"]);

        byte[] result =
            encoder.EncodeReplConf(
                command);

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "*3\r\n" +
            "$8\r\n" +
            "REPLCONF\r\n" +
            "$14\r\n" +
            "listening-port\r\n" +
            "$4\r\n" +
            "6380\r\n",
            actual);
    }

    [Fact]
    public void EncodeReplConf_ShouldEncodeCapability() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        var command =
            new ReplConfCommand(
                "capa",
                ["psync2"]);

        byte[] result =
            encoder.EncodeReplConf(
                command);

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "*3\r\n" +
            "$8\r\n" +
            "REPLCONF\r\n" +
            "$4\r\n" +
            "capa\r\n" +
            "$6\r\n" +
            "psync2\r\n",
            actual);
    }

    [Fact]
    public void EncodePsync_ShouldEncodeInitialSynchronization() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        var command =
            new PsyncCommand(
                "?",
                -1);

        byte[] result =
            encoder.EncodePsync(
                command);

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "*3\r\n" +
            "$5\r\n" +
            "PSYNC\r\n" +
            "$1\r\n" +
            "?\r\n" +
            "$2\r\n" +
            "-1\r\n",
            actual);
    }

    [Fact]
    public void EncodePsync_ShouldEncodeKnownOffset() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        var command =
            new PsyncCommand(
                "replication-id",
                500);

        byte[] result =
            encoder.EncodePsync(
                command);

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "*3\r\n" +
            "$5\r\n" +
            "PSYNC\r\n" +
            "$14\r\n" +
            "replication-id\r\n" +
            "$3\r\n" +
            "500\r\n",
            actual);
    }

    [Fact]
    public void EncodeFullResync_ShouldEncodeSimpleString() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        var response =
            new FullResyncResponse(
                "replication-id",
                1000);

        byte[] result =
            encoder.EncodeFullResync(
                response);

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "+FULLRESYNC replication-id 1000\r\n",
            actual);
    }

    [Fact]
    public void EncodeContinue_ShouldEncodeContinueResponse() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        byte[] result =
            encoder.EncodeContinue();

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "+CONTINUE\r\n",
            actual);
    }

    [Fact]
    public void EncodeAck_ShouldEncodeAcknowledgement() {
        var encoder =
            new RespReplicationProtocolEncoder(
                _serializer);

        var acknowledgement =
            new ReplicationAck(
                12345);

        byte[] result =
            encoder.EncodeAck(
                acknowledgement);

        string actual =
            Encoding.UTF8.GetString(result);

        Assert.Equal(
            "*3\r\n" +
            "$8\r\n" +
            "REPLCONF\r\n" +
            "$3\r\n" +
            "ACK\r\n" +
            "$5\r\n" +
            "12345\r\n",
            actual);
    }
}