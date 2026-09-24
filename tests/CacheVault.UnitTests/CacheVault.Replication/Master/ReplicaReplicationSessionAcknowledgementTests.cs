using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.Replication.Master;

public sealed class ReplicaReplicationSessionAcknowledgementTests {
    [Fact]
    public async Task ProcessAsync_AckAfterSynchronization_ShouldUpdateReplicaAcknowledgedOffset() {
        TestFixture fixture =
            CreateFixture();

        await fixture.CompletePartialSynchronizationAsync();

        Assert.Equal(
            0,
            fixture.Replica.AcknowledgedOffset);

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(100));

        Assert.Equal(
            100,
            fixture.Replica.AcknowledgedOffset);
    }

    [Fact]
    public async Task ProcessAsync_MultipleAcks_ShouldTrackLatestAcknowledgedOffset() {
        TestFixture fixture =
            CreateFixture();

        await fixture.CompletePartialSynchronizationAsync();

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(100));

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(250));

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(500));

        Assert.Equal(
            500,
            fixture.Replica.AcknowledgedOffset);
    }

    [Fact]
    public async Task ProcessAsync_OutOfOrderAck_ShouldNotMoveAcknowledgedOffsetBackward() {
        TestFixture fixture =
            CreateFixture();

        await fixture.CompletePartialSynchronizationAsync();

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(500));

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(300));

        Assert.Equal(
            500,
            fixture.Replica.AcknowledgedOffset);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateAck_ShouldLeaveAcknowledgedOffsetUnchanged() {
        TestFixture fixture =
            CreateFixture();

        await fixture.CompletePartialSynchronizationAsync();

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(500));

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(500));

        Assert.Equal(
            500,
            fixture.Replica.AcknowledgedOffset);
    }

    [Fact]
    public async Task ProcessAsync_NegativeAck_ShouldThrow() {
        TestFixture fixture =
            CreateFixture();

        await fixture.CompletePartialSynchronizationAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await fixture.Session.ProcessAsync(
                    CreateReplConfAckCommand(-1)));
    }

    [Fact]
    public async Task ProcessAsync_AckBeforeSynchronization_ShouldThrow() {
        TestFixture fixture =
            CreateFixture();

        fixture.Session.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await fixture.Session.ProcessAsync(
                    CreateReplConfAckCommand(100)));
    }

    [Fact]
    public async Task ProcessAsync_AckAfterSynchronization_ShouldNotWriteAdditionalResponse() {
        TestFixture fixture =
            CreateFixture();

        await fixture.CompletePartialSynchronizationAsync();

        fixture.Transport.ClearWrittenData();

        await fixture.Session.ProcessAsync(
            CreateReplConfAckCommand(100));

        Assert.Empty(
            fixture.Transport.WrittenData);
    }

    private static TestFixture CreateFixture() {
        var replicationState =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-replication-id");

        var backlog =
            new ReplicationBacklog(
                4096);

        RespReplicationCommandEncoder encoder =
            CreateEncoder();

        ReplicationEntry entry =
            encoder.Encode(
                0,
                "SET",
                ["key", "value"]);

        backlog.Append(
            entry);

        replicationState.AdvanceReplicationOffset(
            entry.Length);

        var replica =
            new ReplicaInfo(
                "replica-1");

        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                replica,
                transport);

        connection.MarkConnected();

        var psyncDecisionService =
            new PsyncDecisionService(
                replicationState,
                backlog);

        var handshake =
            new ReplicationHandshake(
                connection,
                psyncDecisionService);

        var snapshotProvider =
            new TestReplicationSnapshotProvider();

        var fullResynchronizationCoordinator =
            new FullResynchronizationCoordinator(
                connection,
                snapshotProvider,
                replicationState,
                backlog);

        var partialResynchronizationCoordinator =
            new PartialResynchronizationCoordinator(
                connection,
                backlog);

        var parser =
            new RespReplicationProtocolParser();

        var replicaRegistry =
            new ReplicaRegistry();

        var session =
            new ReplicaReplicationSession(
                connection,
                handshake,
                fullResynchronizationCoordinator,
                partialResynchronizationCoordinator,
                parser,
                replicaRegistry);

        return new TestFixture(
            session,
            replica,
            connection,
            transport,
            replicationState);
    }

    private static RespReplicationCommandEncoder CreateEncoder() {
        return new RespReplicationCommandEncoder(
            new RespSerializer());
    }

    private static RespArray CreateReplConfAckCommand(
        long offset) {
        return new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("ACK"),
                new RespBulkString(offset.ToString())
            ]);
    }

    private sealed class TestFixture {
        public TestFixture(
            ReplicaReplicationSession session,
            ReplicaInfo replica,
            ReplicaConnection connection,
            TestReplicationTransport transport,
            ReplicationState replicationState) {
            Session =
                session;

            Replica =
                replica;

            Connection =
                connection;

            Transport =
                transport;

            ReplicationState =
                replicationState;
        }

        public ReplicaReplicationSession Session { get; }

        public ReplicaInfo Replica { get; }

        public ReplicaConnection Connection { get; }

        public TestReplicationTransport Transport { get; }

        public ReplicationState ReplicationState { get; }

        public async Task CompletePartialSynchronizationAsync() {
            Session.Start();

            await Session.ProcessAsync(
                CreateReplConfListeningPortCommand());

            await Session.ProcessAsync(
                CreateReplConfCapabilityCommand());

            await Session.ProcessAsync(
                CreatePsyncCommand(
                    ReplicationState.ReplicationId,
                    0));

            Assert.Equal(
                ReplicationHandshakeState.Completed,
                Session.State);

            Assert.Equal(
                ReplicaConnectionState.Online,
                Connection.State);
        }
    }

    private static RespArray CreateReplConfListeningPortCommand() {
        return new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("listening-port"),
                new RespBulkString("6379")
            ]);
    }

    private static RespArray CreateReplConfCapabilityCommand() {
        return new RespArray(
            [
                new RespBulkString("REPLCONF"),
                new RespBulkString("capa"),
                new RespBulkString("psync2")
            ]);
    }

    private static RespArray CreatePsyncCommand(
        string replicationId,
        long offset) {
        return new RespArray(
            [
                new RespBulkString("PSYNC"),
                new RespBulkString(replicationId),
                new RespBulkString(offset.ToString())
            ]);
    }

    private sealed class TestReplicationSnapshotProvider :
    IReplicationSnapshotProvider {
        public ValueTask WriteSnapshotAsync(
            Stream destination,
            CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(
                destination);

            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestReplicationTransport :
        IReplicationTransport {
        private readonly List<byte[]> _writtenData = [];

        public IReadOnlyList<byte[]> WrittenData =>
            _writtenData;

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) {
            _writtenData.Add(
                data.ToArray());

            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(
                0);
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }

        public void ClearWrittenData() {
            _writtenData.Clear();
        }
    }
}