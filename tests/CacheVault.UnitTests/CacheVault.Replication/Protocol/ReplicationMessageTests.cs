using CacheVault.Replication.Protocol;

namespace CacheVault.UnitTests.Replication.Protocol;

public sealed class ReplicationMessageTests {
    [Fact]
    public void ReplConfCommand_ShouldStoreValues() {
        var command =
            new ReplConfCommand(
                "listening-port",
                ["6380"]);

        Assert.Equal(
            "listening-port",
            command.SubCommand);

        Assert.Equal(
            ["6380"],
            command.Arguments);
    }

    [Fact]
    public void ReplConfCommand_ShouldSupportCapability() {
        var command =
            new ReplConfCommand(
                "capa",
                ["psync2"]);

        Assert.Equal(
            "capa",
            command.SubCommand);

        Assert.Single(
            command.Arguments);

        Assert.Equal(
            "psync2",
            command.Arguments[0]);
    }

    [Fact]
    public void PsyncCommand_ShouldStoreReplicationIdAndOffset() {
        var command =
            new PsyncCommand(
                "?",
                -1);

        Assert.Equal(
            "?",
            command.ReplicationId);

        Assert.Equal(
            -1,
            command.Offset);
    }

    [Fact]
    public void PsyncCommand_ShouldSupportKnownReplicationId() {
        const string replicationId =
            "0123456789abcdef0123456789abcdef";

        var command =
            new PsyncCommand(
                replicationId,
                500);

        Assert.Equal(
            replicationId,
            command.ReplicationId);

        Assert.Equal(
            500,
            command.Offset);
    }

    [Fact]
    public void FullResyncResponse_ShouldStoreValues() {
        const string replicationId =
            "0123456789abcdef0123456789abcdef";

        var response =
            new FullResyncResponse(
                replicationId,
                1000);

        Assert.Equal(
            replicationId,
            response.ReplicationId);

        Assert.Equal(
            1000,
            response.ReplicationOffset);
    }

    [Fact]
    public void ContinueResponse_ShouldBeConstructible() {
        var response =
            new ContinueResponse();

        Assert.NotNull(
            response);
    }

    [Fact]
    public void ReplicationAck_ShouldStoreOffset() {
        var ack =
            new ReplicationAck(
                500);

        Assert.Equal(
            500,
            ack.Offset);
    }

    [Fact]
    public void ReplicationAck_ShouldSupportZeroOffset() {
        var ack =
            new ReplicationAck(
                0);

        Assert.Equal(
            0,
            ack.Offset);
    }
}