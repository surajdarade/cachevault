using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.Replication.Master;

public sealed class ReplicaConnectionTests {
    [Fact]
    public void Constructor_ShouldStartDisconnected() {
        var replica =
            new ReplicaInfo(
                "replica-1");

        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                replica,
                transport);

        Assert.Same(
            replica,
            connection.Replica);

        Assert.Equal(
            ReplicaConnectionState.Disconnected,
            connection.State);
    }

    [Fact]
    public void MarkConnected_ShouldTransitionToConnected() {
        ReplicaConnection connection =
            CreateConnection();

        connection.MarkConnected();

        Assert.Equal(
            ReplicaConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public void BeginHandshake_ShouldTransitionFromConnected() {
        ReplicaConnection connection =
            CreateConnection();

        connection.MarkConnected();

        connection.BeginHandshake();

        Assert.Equal(
            ReplicaConnectionState.Handshaking,
            connection.State);
    }

    [Fact]
    public void BeginSynchronization_ShouldTransitionFromHandshake() {
        ReplicaConnection connection =
            CreateConnection();

        connection.MarkConnected();

        connection.BeginHandshake();

        connection.BeginSynchronization();

        Assert.Equal(
            ReplicaConnectionState.Synchronizing,
            connection.State);
    }

    [Fact]
    public void MarkOnline_ShouldTransitionFromSynchronization() {
        ReplicaConnection connection =
            CreateConnection();

        connection.MarkConnected();

        connection.BeginHandshake();

        connection.BeginSynchronization();

        connection.MarkOnline();

        Assert.Equal(
            ReplicaConnectionState.Online,
            connection.State);
    }

    [Fact]
    public void MarkDisconnected_ShouldReturnToDisconnected() {
        ReplicaConnection connection =
            CreateConnection();

        connection.MarkConnected();

        connection.BeginHandshake();

        connection.BeginSynchronization();

        connection.MarkOnline();

        connection.MarkDisconnected();

        Assert.Equal(
            ReplicaConnectionState.Disconnected,
            connection.State);
    }

    [Fact]
    public void InvalidTransition_ShouldThrow() {
        ReplicaConnection connection =
            CreateConnection();

        Assert.Throws<InvalidOperationException>(
            () =>
                connection.MarkOnline());
    }

    [Fact]
    public void ConnectedToSynchronization_ShouldThrow() {
        ReplicaConnection connection =
            CreateConnection();

        connection.MarkConnected();

        Assert.Throws<InvalidOperationException>(
            () =>
                connection.BeginSynchronization());
    }

    [Fact]
    public void HandshakeWithoutConnectedState_ShouldThrow() {
        ReplicaConnection connection =
            CreateConnection();

        Assert.Throws<InvalidOperationException>(
            () =>
                connection.BeginHandshake());
    }

    [Fact]
    public async Task WriteAsync_ShouldDelegateToTransport() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        byte[] payload =
            [1, 2, 3, 4];

        connection.MarkConnected();

        await connection.WriteAsync(
            payload);

        Assert.Equal(
            payload,
            transport.LastWrittenData);
    }

    [Fact]
    public async Task ReadAsync_ShouldDelegateToTransport() {
        var transport =
            new TestReplicationTransport(
                [5, 6, 7]);

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        byte[] buffer =
            new byte[3];

        int bytesRead =
            await connection.ReadAsync(
                buffer);

        Assert.Equal(
            3,
            bytesRead);

        Assert.Equal(
            [5, 6, 7],
            buffer);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeTransport() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        await connection.DisposeAsync();

        Assert.True(
            transport.IsDisposed);

        Assert.Equal(
            ReplicaConnectionState.Disconnected,
            connection.State);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.Equal(
            1,
            transport.DisposeCount);
    }

    [Fact]
    public async Task WriteAsync_AfterDispose_ShouldThrow() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        await connection.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () =>
                await connection.WriteAsync(
                    new byte[] { 1 }));
    }

    [Fact]
    public async Task ReadAsync_AfterDispose_ShouldThrow() {
        var transport =
            new TestReplicationTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        await connection.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () =>
                await connection.ReadAsync(
                    new byte[1]));
    }

    [Fact]
    public async Task WriteAsync_ShouldSerializeConcurrentWrites() {
        var transport =
            new BlockingWriteTransport();

        var connection =
            new ReplicaConnection(
                new ReplicaInfo(
                    "replica-1"),
                transport);

        connection.MarkConnected();

        Task firstWrite =
            connection.WriteAsync(
                "first"u8.ToArray()).AsTask();

        await transport.FirstWriteStarted;

        Task secondWrite =
            connection.WriteAsync(
                "second"u8.ToArray()).AsTask();

        await Task.Delay(50);

        Assert.False(
            secondWrite.IsCompleted);

        transport.ReleaseFirstWrite();

        await Task.WhenAll(
            firstWrite,
            secondWrite);

        Assert.Equal(
            [
                "first",
            "second"
            ],
            transport.WrittenData);
    }

    private static ReplicaConnection CreateConnection() {
        return new ReplicaConnection(
            new ReplicaInfo(
                "replica-1"),
            new TestReplicationTransport());
    }

    private sealed class BlockingWriteTransport :
    IReplicationTransport {
        private readonly TaskCompletionSource<bool>
            _firstWriteStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _releaseFirstWrite =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly List<string> _writtenData = [];

        private int _writeCount;

        public Task FirstWriteStarted =>
            _firstWriteStarted.Task;

        public IReadOnlyList<string> WrittenData =>
            _writtenData;

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) {
            int writeNumber =
                Interlocked.Increment(
                    ref _writeCount);

            return WriteInternalAsync(
                data,
                writeNumber,
                cancellationToken);
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(0);
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }

        public void ReleaseFirstWrite() {
            _releaseFirstWrite.TrySetResult(true);
        }

        private async ValueTask WriteInternalAsync(
            ReadOnlyMemory<byte> data,
            int writeNumber,
            CancellationToken cancellationToken) {
            if (writeNumber == 1) {
                _firstWriteStarted.TrySetResult(true);

                await _releaseFirstWrite.Task
                    .WaitAsync(cancellationToken);
            }

            _writtenData.Add(
                System.Text.Encoding.UTF8.GetString(
                    data.Span));
        }
    }

    private sealed class TestReplicationTransport :
        IReplicationTransport {
        private readonly byte[] _readData;

        public TestReplicationTransport(
            byte[]? readData = null) {
            _readData =
                readData ?? [];
        }

        public bool IsDisposed { get; private set; }

        public int DisposeCount { get; private set; }

        public byte[] LastWrittenData { get; private set; } = [];

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) {
            LastWrittenData =
                data.ToArray();

            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            int count =
                Math.Min(
                    buffer.Length,
                    _readData.Length);

            _readData.AsSpan(
                0,
                count)
                .CopyTo(
                    buffer.Span);

            return ValueTask.FromResult(
                count);
        }

        public ValueTask DisposeAsync() {
            DisposeCount++;

            IsDisposed = true;

            return ValueTask.CompletedTask;
        }
    }
}