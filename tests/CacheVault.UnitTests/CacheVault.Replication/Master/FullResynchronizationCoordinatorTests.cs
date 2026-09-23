using System.Text;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class FullResynchronizationCoordinatorTests {
    [Fact]
    public async Task ExecuteAsync_ShouldSendFullResyncHeader() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        var snapshotProvider =
            new TestSnapshotProvider(
                "CVDB-test-snapshot"u8.ToArray());

        var coordinator =
            CreateCoordinator(
                connection,
                snapshotProvider,
                state,
                backlog);

        await coordinator.ExecuteAsync();

        string written =
            Encoding.UTF8.GetString(
                transport.WrittenData.ToArray());

        Assert.StartsWith(
            "+FULLRESYNC master-id 100\r\n",
            written);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendSnapshotAfterHeader() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        byte[] snapshot =
            "CVDB-test-snapshot"u8.ToArray();

        var snapshotProvider =
            new TestSnapshotProvider(
                snapshot);

        var coordinator =
            CreateCoordinator(
                connection,
                snapshotProvider,
                state,
                backlog);

        await coordinator.ExecuteAsync();

        byte[] expectedHeader =
            Encoding.UTF8.GetBytes(
                "+FULLRESYNC master-id 100\r\n");

        byte[] actual =
            transport.WrittenData.ToArray();

        Assert.True(
            actual.Length >=
            expectedHeader.Length +
            snapshot.Length);

        Assert.Equal(
            expectedHeader,
            actual[..expectedHeader.Length]);

        Assert.Equal(
            snapshot,
            actual[
                expectedHeader.Length..(expectedHeader.Length +
                 snapshot.Length)]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendBacklogAfterSnapshot() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        byte[] snapshot =
            "CVDB"u8.ToArray();

        var snapshotProvider =
            new TestSnapshotProvider(
                snapshot);

        var catchUpData =
            "0123456789"u8.ToArray();

        var snapshotAndCatchUpProvider =
            new SnapshotProviderThatAppendsToBacklog(
                snapshotProvider,
                backlog,
                catchUpData);

        var coordinator =
            CreateCoordinator(
                connection,
                snapshotAndCatchUpProvider,
                state,
                backlog);

        await coordinator.ExecuteAsync();

        byte[] header =
            Encoding.UTF8.GetBytes(
                "+FULLRESYNC master-id 100\r\n");

        byte[] expected =
            header
                .Concat(snapshot)
                .Concat(catchUpData)
                .ToArray();

        Assert.Equal(
            expected,
            transport.WrittenData.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCaptureOffsetBeforeSnapshotGeneration() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        var snapshotProvider =
            new SnapshotProviderThatAdvancesReplicationState(
                state,
                backlog);

        var coordinator =
            CreateCoordinator(
                connection,
                snapshotProvider,
                state,
                backlog);

        await coordinator.ExecuteAsync();

        byte[] written =
            transport.WrittenData.ToArray();

        string header =
            Encoding.UTF8.GetString(
                written);

        Assert.StartsWith(
            "+FULLRESYNC master-id 100\r\n",
            header);

        Assert.Equal(
            ReplicaConnectionState.Online,
            connection.State);

        Assert.Equal(
            110,
            state.ReplicationOffset);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMoveConnectionOnline() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        var coordinator =
            CreateCoordinator(
                connection,
                new TestSnapshotProvider(
                    "snapshot"u8.ToArray()),
                state,
                backlog);

        await coordinator.ExecuteAsync();

        Assert.Equal(
            ReplicaConnectionState.Online,
            connection.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBacklogLostBoundary_ShouldThrow() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            new ReplicationBacklog(5);

        var coordinator =
            CreateCoordinator(
                connection,
                new TestSnapshotProvider(
                    "snapshot"u8.ToArray()),
                state,
                backlog);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await coordinator.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionIsNotSynchronizing_ShouldThrow() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateConnectedConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        var coordinator =
            CreateCoordinator(
                connection,
                new TestSnapshotProvider(
                    "snapshot"u8.ToArray()),
                state,
                backlog);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await coordinator.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrow() {
        var transport =
            new TestReplicationTransport();

        var connection =
            CreateSynchronizingConnection(
                transport);

        var state =
            CreateReplicationState();

        var backlog =
            CreateBacklog();

        var coordinator =
            CreateCoordinator(
                connection,
                new TestSnapshotProvider(
                    "snapshot"u8.ToArray()),
                state,
                backlog);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await coordinator.ExecuteAsync(
                    cancellationTokenSource.Token));
    }

    private static FullResynchronizationCoordinator
        CreateCoordinator(
            ReplicaConnection connection,
            IReplicationSnapshotProvider snapshotProvider,
            IReplicationState state,
            IReplicationBacklog backlog) {
        return new FullResynchronizationCoordinator(
            connection,
            snapshotProvider,
            state,
            backlog);
    }

    private static ReplicationState
        CreateReplicationState() {
        var state =
            new ReplicationState(
                isMaster: true,
                replicationId: "master-id");

        state.AdvanceReplicationOffset(
            100);

        return state;
    }

    private static IReplicationBacklog
        CreateBacklog() {
        var backlog =
            new ReplicationBacklog(
                1024);

        var initialEntry =
            new ReplicationEntry(
                0,
                100,
                new byte[100]);

        backlog.Append(
            initialEntry);

        return backlog;
    }

    private static ReplicaConnection
        CreateSynchronizingConnection(
            TestReplicationTransport transport) {
        var connection =
            CreateConnectedConnection(
                transport);

        connection.BeginHandshake();
        connection.BeginSynchronization();

        return connection;
    }

    private static ReplicaConnection
        CreateConnectedConnection(
            TestReplicationTransport transport) {
        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        return connection;
    }

    private sealed class TestSnapshotProvider :
        IReplicationSnapshotProvider {
        private readonly byte[] _snapshot;

        public TestSnapshotProvider(
            byte[] snapshot) {
            _snapshot = snapshot;
        }

        public async ValueTask WriteSnapshotAsync(
            Stream destination,
            CancellationToken cancellationToken = default) {
            await destination.WriteAsync(
                _snapshot,
                cancellationToken);
        }
    }

    private sealed class SnapshotProviderThatAdvancesReplicationState :
        IReplicationSnapshotProvider {
        private readonly ReplicationState _state;
        private readonly IReplicationBacklog _backlog;

        public SnapshotProviderThatAdvancesReplicationState(
            ReplicationState state,
            IReplicationBacklog backlog) {
            _state = state;
            _backlog = backlog;
        }

        public async ValueTask WriteSnapshotAsync(
            Stream destination,
            CancellationToken cancellationToken = default) {
            await destination.WriteAsync(
                "snapshot"u8.ToArray(),
                cancellationToken);

            var entry =
                new ReplicationEntry(
                    100,
                    110,
                    "0123456789"u8.ToArray());

            _backlog.Append(
                entry);

            _state.AdvanceReplicationOffset(
                entry.Length);
        }
    }

    private sealed class SnapshotProviderThatAppendsToBacklog :
        IReplicationSnapshotProvider {
        private readonly IReplicationSnapshotProvider
            _innerProvider;

        private readonly IReplicationBacklog _backlog;
        private readonly byte[] _catchUpData;

        public SnapshotProviderThatAppendsToBacklog(
            IReplicationSnapshotProvider innerProvider,
            IReplicationBacklog backlog,
            byte[] catchUpData) {
            _innerProvider =
                innerProvider;

            _backlog =
                backlog;

            _catchUpData =
                catchUpData;
        }

        public async ValueTask WriteSnapshotAsync(
            Stream destination,
            CancellationToken cancellationToken = default) {
            await _innerProvider.WriteSnapshotAsync(
                destination,
                cancellationToken);

            var entry =
                new ReplicationEntry(
                    100,
                    110,
                    _catchUpData);

            _backlog.Append(
                entry);
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