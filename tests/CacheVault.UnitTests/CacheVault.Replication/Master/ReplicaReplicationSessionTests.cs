using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class ReplicaReplicationSessionTests {
    [Fact]
    public void Start_ShouldStartHandshake() {
        var fixture =
            CreateFixture();

        fixture.Session.Start();

        Assert.Equal(
            ReplicationHandshakeState.WaitingForListeningPort,
            fixture.Session.State);

        Assert.Equal(
            ReplicaConnectionState.Handshaking,
            fixture.Connection.State);
    }

    [Fact]
    public async Task ProcessReplConfListeningPort_ShouldReturnOk() {
        var fixture =
            CreateFixture();

        fixture.Session.Start();

        await fixture.Session.ProcessAsync(
            CreateReplConf(
                "listening-port",
                "6380"));

        Assert.Equal(
            "+OK\r\n"u8.ToArray(),
            fixture.Transport.WrittenData);
    }

    [Fact]
    public async Task ProcessReplConfCapabilities_ShouldReturnOk() {
        var fixture =
            CreateFixture();

        fixture.Session.Start();

        await fixture.Session.ProcessAsync(
            CreateReplConf(
                "listening-port",
                "6380"));

        await fixture.Session.ProcessAsync(
            CreateReplConf(
                "capa",
                "psync2"));

        Assert.Equal(
            "+OK\r\n+OK\r\n"u8.ToArray(),
            fixture.Transport.WrittenData);
    }

    [Fact]
    public async Task ProcessPsync_ShouldPerformFullResynchronization() {
        var fixture =
            CreateFixture();

        fixture.Session.Start();

        await fixture.Session.ProcessAsync(
            CreateReplConf(
                "listening-port",
                "6380"));

        await fixture.Session.ProcessAsync(
            CreateReplConf(
                "capa",
                "psync2"));

        await fixture.Session.ProcessAsync(
            new RespArray(
            [
                new RespBulkString("PSYNC"),
            new RespBulkString("?"),
            new RespBulkString("-1")
            ]));

        byte[] expectedOkResponses =
            "+OK\r\n+OK\r\n"u8.ToArray();

        Assert.Equal(
            expectedOkResponses,
            fixture.Transport.WrittenData
                .Take(expectedOkResponses.Length)
                .ToArray());

        byte[] fullResyncHeader =
            "+FULLRESYNC master-id 100\r\n"u8.ToArray();

        byte[] writtenData =
            fixture.Transport.WrittenData.ToArray();

        int headerStart =
            expectedOkResponses.Length;

        Assert.True(
            writtenData.Length >=
            headerStart + fullResyncHeader.Length);

        Assert.True(
            writtenData
                .Skip(headerStart)
                .Take(fullResyncHeader.Length)
                .SequenceEqual(
                    fullResyncHeader));

        Assert.Equal(
            ReplicaConnectionState.Online,
            fixture.Connection.State);

        Assert.Equal(
            ReplicationHandshakeState.Completed,
            fixture.Session.State);
    }

    [Fact]
    public async Task ProcessUnsupportedCommand_ShouldThrow() {
        var fixture =
            CreateFixture();

        fixture.Session.Start();

        var command =
            new RespArray(
            [
                new RespBulkString("PING")
            ]);

        await Assert.ThrowsAsync<FormatException>(
            async () =>
                await fixture.Session.ProcessAsync(
                    command));
    }

    [Fact]
    public async Task ProcessInvalidMessage_ShouldThrow() {
        var fixture =
            CreateFixture();

        fixture.Session.Start();

        await Assert.ThrowsAsync<FormatException>(
            async () =>
                await fixture.Session.ProcessAsync(
                    new RespSimpleString(
                        "PING")));
    }

    private static RespArray CreateReplConf(
        string subCommand,
        string argument) {
        return new RespArray(
        [
            new RespBulkString("REPLCONF"),
            new RespBulkString(subCommand),
            new RespBulkString(argument)
        ]);
    }

    private static TestFixture CreateFixture() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        state.AdvanceReplicationOffset(
            100);

        var backlog =
            new ReplicationBacklog(
                1024);

        backlog.Append(
            new ReplicationEntry(
                0,
                100,
                new byte[100]));

        var decisionService =
            new PsyncDecisionService(
                state,
                backlog);

        var handshake =
            new ReplicationHandshake(
                connection,
                decisionService);

        var snapshotProvider =
            new TestSnapshotProvider();

        var coordinator =
            new FullResynchronizationCoordinator(
                connection,
                snapshotProvider,
                state,
                backlog);

        var partialCoordinator =
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
                coordinator,
                partialCoordinator,
                parser,
                replicaRegistry);

        return new TestFixture(
            session,
            connection,
            transport);
    }

    private static TestFixture CreatePartialSyncFixture() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        state.AdvanceReplicationOffset(
            10);

        var backlog =
            new ReplicationBacklog(
                1024);

        backlog.Append(
            new ReplicationEntry(
                0,
                10,
                "0123456789"u8.ToArray()));

        var decisionService =
            new PsyncDecisionService(
                state,
                backlog);

        var handshake =
            new ReplicationHandshake(
                connection,
                decisionService);

        var snapshotProvider =
            new TestSnapshotProvider();

        var fullCoordinator =
            new FullResynchronizationCoordinator(
                connection,
                snapshotProvider,
                state,
                backlog);

        var partialCoordinator =
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
                fullCoordinator,
                partialCoordinator,
                parser,
                replicaRegistry);

        return new TestFixture(
            session,
            connection,
            transport);
    }

    private sealed record TestFixture(
        ReplicaReplicationSession Session,
        ReplicaConnection Connection,
        TestReplicationTransport Transport);

    private sealed class TestSnapshotProvider :
        IReplicationSnapshotProvider {
        public async ValueTask WriteSnapshotAsync(
            Stream destination,
            CancellationToken cancellationToken = default) {
            await destination.WriteAsync(
                "CVDB-test"u8.ToArray(),
                cancellationToken);
        }
    }

    private sealed class TestReplicationTransport :
        IReplicationTransport {
        private readonly List<byte> _writtenData = [];

        public IReadOnlyList<byte> WrittenData =>
            _writtenData;

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            _writtenData.AddRange(
                data.ToArray());

            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(0);
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }
}