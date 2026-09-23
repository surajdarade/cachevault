using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class ReplicationHandshakeTests {
    [Fact]
    public void Start_ShouldMoveToWaitingForListeningPort() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        handshake.Start();

        Assert.Equal(
            ReplicationHandshakeState.WaitingForListeningPort,
            handshake.State);

        Assert.Equal(
            ReplicaConnectionState.Handshaking,
            connection.State);
    }

    [Fact]
    public void ListeningPort_ShouldMoveToWaitingForCapabilities() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        handshake.Start();

        handshake.ProcessReplConf(
            new ReplConfCommand(
                "listening-port",
                ["6380"]));

        Assert.Equal(
            ReplicationHandshakeState.WaitingForCapabilities,
            handshake.State);
    }

    [Fact]
    public void Capabilities_ShouldMoveToWaitingForPsync() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        handshake.Start();

        handshake.ProcessReplConf(
            new ReplConfCommand(
                "listening-port",
                ["6380"]));

        handshake.ProcessReplConf(
            new ReplConfCommand(
                "capa",
                ["psync2"]));

        Assert.Equal(
            ReplicationHandshakeState.WaitingForPsync,
            handshake.State);
    }

    [Fact]
    public void PsyncWithUnknownId_ShouldRequireFullResynchronization() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        StartUntilPsync(
            handshake);

        ReplicationHandshakeResult result =
            handshake.ProcessPsync(
                new PsyncCommand(
                    "?",
                    -1));

        Assert.Equal(
            ReplicationHandshakeState.FullResynchronization,
            result.FinalState);

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Equal(
            "replica-1",
            result.ReplicaId);

        Assert.Equal(
            -1,
            result.ReplicationOffset);

        Assert.Equal(
            ReplicaConnectionState.Synchronizing,
            connection.State);
    }

    [Fact]
    public void PsyncWithDifferentId_ShouldRequireFullResynchronization() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        StartUntilPsync(
            handshake);

        ReplicationHandshakeResult result =
            handshake.ProcessPsync(
                new PsyncCommand(
                    "different-id",
                    50));

        Assert.True(
            result.RequiresFullResynchronization);

        Assert.Equal(
            ReplicationHandshakeState.FullResynchronization,
            result.FinalState);

        Assert.Equal(
            50,
            result.ReplicationOffset);
    }

    [Fact]
    public void PsyncWithMatchingId_ShouldBeginPartialResynchronization() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(
                connection,
                backlogCapacity: 1024,
                populateBacklog: true);

        StartUntilPsync(
            handshake);

        ReplicationHandshakeResult result =
            handshake.ProcessPsync(
                new PsyncCommand(
                    "master-id",
                    5));

        Assert.Equal(
            ReplicationHandshakeState.PartialResynchronization,
            result.FinalState);

        Assert.False(
            result.RequiresFullResynchronization);

        Assert.Equal(
            5,
            result.ReplicationOffset);

        Assert.Equal(
            ReplicaConnectionState.Synchronizing,
            connection.State);
    }

    [Fact]
    public void PsyncOffsetOutsideBacklog_ShouldRequireFullResynchronization() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(
                connection,
                backlogCapacity: 5,
                populateBacklog: true);

        StartUntilPsync(
            handshake);

        ReplicationHandshakeResult result =
            handshake.ProcessPsync(
                new PsyncCommand(
                    "master-id",
                    0));

        Assert.Equal(
            ReplicationHandshakeState.FullResynchronization,
            result.FinalState);

        Assert.True(
            result.RequiresFullResynchronization);
    }

    [Fact]
    public void Complete_ShouldMoveConnectionOnline() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        StartUntilPsync(
            handshake);

        handshake.ProcessPsync(
            new PsyncCommand(
                "?",
                -1));

        handshake.Complete();

        Assert.Equal(
            ReplicationHandshakeState.Completed,
            handshake.State);

        Assert.Equal(
            ReplicaConnectionState.Online,
            connection.State);
    }

    [Fact]
    public void MissingPsync2Capability_ShouldThrow() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        handshake.Start();

        handshake.ProcessReplConf(
            new ReplConfCommand(
                "listening-port",
                ["6380"]));

        Assert.Throws<InvalidOperationException>(
            () =>
                handshake.ProcessReplConf(
                    new ReplConfCommand(
                        "capa",
                        ["eof"])));
    }

    [Fact]
    public void InvalidListeningPort_ShouldThrow() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        handshake.Start();

        Assert.Throws<FormatException>(
            () =>
                handshake.ProcessReplConf(
                    new ReplConfCommand(
                        "listening-port",
                        ["99999"])));
    }

    [Fact]
    public void Fail_ShouldDisconnectConnection() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        handshake.Start();

        handshake.Fail();

        Assert.Equal(
            ReplicationHandshakeState.Failed,
            handshake.State);

        Assert.Equal(
            ReplicaConnectionState.Disconnected,
            connection.State);
    }

    [Fact]
    public void Complete_WhenConnectionIsAlreadyOnline_ShouldNotThrow() {
        var connection =
            CreateConnectedConnection();

        var handshake =
            CreateHandshake(connection);

        StartUntilPsync(
            handshake);

        handshake.ProcessPsync(
            new PsyncCommand(
                "?",
                -1));

        connection.MarkOnline();

        handshake.Complete();

        Assert.Equal(
            ReplicationHandshakeState.Completed,
            handshake.State);

        Assert.Equal(
            ReplicaConnectionState.Online,
            connection.State);
    }

    private static ReplicationHandshake CreateHandshake(
        ReplicaConnection connection,
        long backlogCapacity = 1024,
        bool populateBacklog = false) {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        var backlog =
            new ReplicationBacklog(
                backlogCapacity);

        if (populateBacklog) {
            var entry =
                new ReplicationEntry(
                    0,
                    10,
                    "0123456789"u8.ToArray());

            backlog.Append(
                entry);

            state.AdvanceReplicationOffset(
                entry.Length);
        }

        var decisionService =
            new PsyncDecisionService(
                state,
                backlog);

        return new ReplicationHandshake(
            connection,
            decisionService);
    }

    private static ReplicaConnection CreateConnectedConnection() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        return connection;
    }

    private static void StartUntilPsync(
        ReplicationHandshake handshake) {
        handshake.Start();

        handshake.ProcessReplConf(
            new ReplConfCommand(
                "listening-port",
                ["6380"]));

        handshake.ProcessReplConf(
            new ReplConfCommand(
                "capa",
                ["psync2"]));
    }

    private sealed class TestReplicationTransport :
        IReplicationTransport {
        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) {
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(0);
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }
}