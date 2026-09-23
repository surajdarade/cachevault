using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.State;

public sealed class ReplicationStateTests {
    [Fact]
    public void Constructor_ShouldInitializeMasterState() {
        var state =
            new ReplicationState();

        Assert.True(
            state.IsMaster);

        Assert.NotEmpty(
            state.ReplicationId);

        Assert.Equal(
            0,
            state.ReplicationOffset);

        Assert.Equal(
            0,
            state.AcknowledgedOffset);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueReplicationIds() {
        var first =
            new ReplicationState();

        var second =
            new ReplicationState();

        Assert.NotEqual(
            first.ReplicationId,
            second.ReplicationId);
    }

    [Fact]
    public void Constructor_WithReplicaRole_ShouldCreateReplicaState() {
        var state =
            new ReplicationState(
                isMaster: false);

        Assert.False(
            state.IsMaster);

        Assert.NotEmpty(
            state.ReplicationId);

        Assert.Equal(
            0,
            state.ReplicationOffset);

        Assert.Equal(
            0,
            state.AcknowledgedOffset);
    }

    [Fact]
    public void Constructor_WithReplicationId_ShouldPreserveReplicationId() {
        const string replicationId =
            "0123456789abcdef0123456789abcdef";

        var state =
            new ReplicationState(
                replicationId: replicationId);

        Assert.Equal(
            replicationId,
            state.ReplicationId);
    }

    [Fact]
    public void AdvanceReplicationOffset_ShouldIncreaseOffset() {
        var state =
            new ReplicationState();

        state.AdvanceReplicationOffset(
            100);

        Assert.Equal(
            100,
            state.ReplicationOffset);

        state.AdvanceReplicationOffset(
            50);

        Assert.Equal(
            150,
            state.ReplicationOffset);
    }

    [Fact]
    public void AdvanceReplicationOffset_WithZero_ShouldThrow() {
        var state =
            new ReplicationState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                state.AdvanceReplicationOffset(
                    0));
    }

    [Fact]
    public void AdvanceReplicationOffset_WithNegativeValue_ShouldThrow() {
        var state =
            new ReplicationState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                state.AdvanceReplicationOffset(
                    -1));
    }

    [Fact]
    public void Acknowledge_ShouldAdvanceAcknowledgedOffset() {
        var state =
            new ReplicationState();

        state.Acknowledge(
            100);

        Assert.Equal(
            100,
            state.AcknowledgedOffset);

        state.Acknowledge(
            150);

        Assert.Equal(
            150,
            state.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_ShouldNeverMoveOffsetBackwards() {
        var state =
            new ReplicationState();

        state.Acknowledge(
            100);

        state.Acknowledge(
            150);

        state.Acknowledge(
            120);

        Assert.Equal(
            150,
            state.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_WithSameOffset_ShouldKeepCurrentOffset() {
        var state =
            new ReplicationState();

        state.Acknowledge(
            100);

        state.Acknowledge(
            100);

        Assert.Equal(
            100,
            state.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_WithNegativeOffset_ShouldThrow() {
        var state =
            new ReplicationState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                state.Acknowledge(
                    -1));
    }

    [Fact]
    public void ReplicationOffset_ShouldRemainIndependentFromAcknowledgedOffset() {
        var state =
            new ReplicationState();

        state.AdvanceReplicationOffset(
            500);

        state.Acknowledge(
            200);

        Assert.Equal(
            500,
            state.ReplicationOffset);

        Assert.Equal(
            200,
            state.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_ShouldNotChangeReplicationOffset() {
        var state =
            new ReplicationState();

        state.AdvanceReplicationOffset(
            500);

        state.Acknowledge(
            500);

        Assert.Equal(
            500,
            state.ReplicationOffset);

        Assert.Equal(
            500,
            state.AcknowledgedOffset);
    }
}