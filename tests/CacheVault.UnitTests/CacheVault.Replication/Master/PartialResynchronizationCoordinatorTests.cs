using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.Replication.Master;

public sealed class PartialResynchronizationCoordinatorTests {
    [Fact]
    public async Task ExecuteAsync_ShouldSendContinue() {
        var fixture =
            CreateFixture();

        await fixture.Coordinator.ExecuteAsync(
            5);

        Assert.Equal(
            "+CONTINUE\r\n"u8.ToArray(),
            fixture.Transport.WrittenData
                .Take(11)
                .ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendBacklogFromRequestedOffset() {
        var fixture =
            CreateFixture();

        await fixture.Coordinator.ExecuteAsync(
            5);

        byte[] expected =
            "+CONTINUE\r\n56789"u8.ToArray();

        Assert.Equal(
            expected,
            fixture.Transport.WrittenData);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMoveConnectionOnline() {
        var fixture =
            CreateFixture();

        await fixture.Coordinator.ExecuteAsync(
            5);

        Assert.Equal(
            ReplicaConnectionState.Online,
            fixture.Connection.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOffsetEqualsEnd_ShouldSendOnlyContinue() {
        var fixture =
            CreateFixture();

        await fixture.Coordinator.ExecuteAsync(
            10);

        Assert.Equal(
            "+CONTINUE\r\n"u8.ToArray(),
            fixture.Transport.WrittenData);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOffsetOutsideBacklog_ShouldThrow() {
        var fixture =
            CreateFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await fixture.Coordinator.ExecuteAsync(
                    11));
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeOffset_ShouldThrow() {
        var fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await fixture.Coordinator.ExecuteAsync(
                    -1));
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionIsNotSynchronizing_ShouldThrow() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        var backlog =
            CreateBacklog();

        var coordinator =
            new PartialResynchronizationCoordinator(
                connection,
                backlog);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await coordinator.ExecuteAsync(
                    5));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrow() {
        var fixture =
            CreateFixture();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await fixture.Coordinator.ExecuteAsync(
                    5,
                    cancellationTokenSource.Token));
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
        connection.BeginHandshake();
        connection.BeginSynchronization();

        var backlog =
            new ReplicationBacklog(
                1024);

        backlog.Append(
            new ReplicationEntry(
                0,
                10,
                "0123456789"u8.ToArray()));

        var coordinator =
            new PartialResynchronizationCoordinator(
                connection,
                backlog);

        return new TestFixture(
            coordinator,
            connection,
            transport);
    }

    private static IReplicationBacklog CreateBacklog() {
        var backlog =
            new ReplicationBacklog(
                5);

        backlog.Append(
            new ReplicationEntry(
                0,
                10,
                "0123456789"u8.ToArray()));

        return backlog;
    }

    private sealed record TestFixture(
        PartialResynchronizationCoordinator Coordinator,
        ReplicaConnection Connection,
        TestReplicationTransport Transport);

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