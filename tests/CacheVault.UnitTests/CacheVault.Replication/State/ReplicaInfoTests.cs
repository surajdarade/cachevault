using CacheVault.Replication.State;

namespace CacheVault.UnitTests.Replication.State;

public sealed class ReplicaInfoTests {
    [Fact]
    public void Constructor_ShouldSetReplicaId() {
        var replica =
            new ReplicaInfo(
                "replica-1");

        Assert.Equal(
            "replica-1",
            replica.ReplicaId);

        Assert.Equal(
            0,
            replica.AcknowledgedOffset);
    }

    [Fact]
    public void Constructor_WithNullId_ShouldThrow() {
        Assert.Throws<ArgumentNullException>(
            () =>
                new ReplicaInfo(
                    null!));
    }

    [Fact]
    public void Constructor_WithEmptyId_ShouldThrow() {
        Assert.Throws<ArgumentException>(
            () =>
                new ReplicaInfo(
                    string.Empty));
    }

    [Fact]
    public void Acknowledge_ShouldAdvanceOffset() {
        var replica =
            new ReplicaInfo(
                "replica-1");

        replica.Acknowledge(
            100);

        Assert.Equal(
            100,
            replica.AcknowledgedOffset);

        replica.Acknowledge(
            250);

        Assert.Equal(
            250,
            replica.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_ShouldNeverMoveOffsetBackwards() {
        var replica =
            new ReplicaInfo(
                "replica-1");

        replica.Acknowledge(
            500);

        replica.Acknowledge(
            300);

        Assert.Equal(
            500,
            replica.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_WithSameOffset_ShouldNotChangeOffset() {
        var replica =
            new ReplicaInfo(
                "replica-1");

        replica.Acknowledge(
            100);

        replica.Acknowledge(
            100);

        Assert.Equal(
            100,
            replica.AcknowledgedOffset);
    }

    [Fact]
    public void Acknowledge_WithNegativeOffset_ShouldThrow() {
        var replica =
            new ReplicaInfo(
                "replica-1");

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                replica.Acknowledge(
                    -1));
    }
}