using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class PsyncDecisionServiceTests {
    [Fact]
    public void UnknownReplicationId_ShouldRequireFullResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                1024);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "different-id",
                    0));

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Empty(
            result.ReplicationData);
    }

    [Fact]
    public void QuestionMarkReplicationId_ShouldRequireFullResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                1024);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "?",
                    -1));

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Empty(
            result.ReplicationData);
    }

    [Fact]
    public void NegativeOffset_ShouldRequireFullResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                1024);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "master-id",
                    -1));

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Empty(
            result.ReplicationData);
    }

    [Fact]
    public void OffsetBeyondMasterOffset_ShouldRequireFullResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                1024);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        state.AdvanceReplicationOffset(100);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "master-id",
                    101));

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Empty(
            result.ReplicationData);
    }

    [Fact]
    public void OffsetOutsideBacklog_ShouldRequireFullResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                5);

        var entry =
            new ReplicationEntry(
                0,
                10,
                "0123456789"u8.ToArray());

        backlog.Append(
            entry);

        state.AdvanceReplicationOffset(
            entry.Length);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "master-id",
                    0));

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Empty(
            result.ReplicationData);
    }

    [Fact]
    public void ValidOffsetInBacklog_ShouldAllowPartialResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                1024);

        var entry =
            new ReplicationEntry(
                0,
                10,
                "0123456789"u8.ToArray());

        backlog.Append(
            entry);

        state.AdvanceReplicationOffset(
            entry.Length);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "master-id",
                    5));

        Assert.False(
            result.RequiresFullResynchronization);

        Assert.Equal(
            5,
            result.RequestedOffset);

        Assert.Equal(
            "56789"u8.ToArray(),
            result.ReplicationData);
    }

    [Fact]
    public void CurrentEndOffset_ShouldAllowEmptyPartialResynchronization() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                1024);

        var entry =
            new ReplicationEntry(
                0,
                10,
                "0123456789"u8.ToArray());

        backlog.Append(
            entry);

        state.AdvanceReplicationOffset(
            entry.Length);

        var service =
            new PsyncDecisionService(
                state,
                backlog);

        PsyncDecision result =
            service.Decide(
                new PsyncCommand(
                    "master-id",
                    10));

        Assert.False(
            result.RequiresFullResynchronization);

        Assert.Empty(
            result.ReplicationData);
    }
}