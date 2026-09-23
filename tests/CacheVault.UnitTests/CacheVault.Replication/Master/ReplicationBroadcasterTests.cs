using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class ReplicationBroadcasterTests {
    [Fact]
    public async Task BroadcastAsync_ShouldWriteToOnlineReplicas() {
        var registry =
            new ReplicaConnectionRegistry();

        var replica1Fixture =
            CreateConnection(
                "replica-1");

        var replica2Fixture =
            CreateConnection(
                "replica-2");

        var replica1 =
            replica1Fixture.Connection;

        var replica2 =
            replica2Fixture.Connection;

        BringOnline(
            replica1);

        BringOnline(
            replica2);

        registry.Register(
            replica1);

        registry.Register(
            replica2);

        var broadcaster =
            new ReplicationBroadcaster(
                registry);

        byte[] data =
            "SET key value\r\n"u8.ToArray();

        var entry =
            new ReplicationEntry(
                0,
                data.Length,
                data);

        await broadcaster.BroadcastAsync(
            entry);

        Assert.Equal(
            data,
            replica1Fixture.Transport.LastWrittenData);

        Assert.Equal(
            data,
            replica2Fixture.Transport.LastWrittenData);
    }

    [Fact]
    public async Task BroadcastAsync_ShouldSkipNonOnlineReplicas() {
        var registry =
            new ReplicaConnectionRegistry();

        var onlineReplicaFixture =
            CreateConnection(
                "replica-1");

        var connectedReplicaFixture =
            CreateConnection(
                "replica-2");

        var onlineReplica =
            onlineReplicaFixture.Connection;

        var connectedReplica =
            connectedReplicaFixture.Connection;

        BringOnline(
            onlineReplica);

        connectedReplica.MarkConnected();

        registry.Register(
            onlineReplica);

        registry.Register(
            connectedReplica);

        var broadcaster =
            new ReplicationBroadcaster(
                registry);

        byte[] data =
            "SET key value\r\n"u8.ToArray();

        var entry =
            new ReplicationEntry(
                0,
                data.Length,
                data);

        await broadcaster.BroadcastAsync(
            entry);

        Assert.Equal(
            data,
            onlineReplicaFixture.Transport.LastWrittenData);

        Assert.Empty(
            connectedReplicaFixture.Transport.LastWrittenData);
    }

    [Fact]
    public async Task BroadcastAsync_WithNoOnlineReplicas_ShouldDoNothing() {
        var registry =
            new ReplicaConnectionRegistry();

        var connectionFixture =
            CreateConnection(
                "replica-1");

        var connection =
            connectionFixture.Connection;

        connection.MarkConnected();

        registry.Register(
            connection);

        var broadcaster =
            new ReplicationBroadcaster(
                registry);

        byte[] data =
            "SET key value\r\n"u8.ToArray();

        var entry =
            new ReplicationEntry(
                0,
                data.Length,
                data);

        await broadcaster.BroadcastAsync(
            entry);

        Assert.Empty(
            connectionFixture.Transport.LastWrittenData);
    }

    [Fact]
    public async Task BroadcastAsync_ShouldPreserveEntryBytes() {
        var registry =
            new ReplicaConnectionRegistry();

        var connectionFixture =
            CreateConnection(
                "replica-1");

        var connection =
            connectionFixture.Connection;

        BringOnline(
            connection);

        registry.Register(
            connection);

        var broadcaster =
            new ReplicationBroadcaster(
                registry);

        byte[] data =
        [
            1,
            2,
            3,
            4,
            5
        ];

        var entry =
            new ReplicationEntry(
                100,
                105,
                data);

        await broadcaster.BroadcastAsync(
            entry);

        Assert.Equal(
            data,
            connectionFixture.Transport.LastWrittenData);
    }

    [Fact]
    public async Task BroadcastAsync_WhenCancelled_ShouldThrow() {
        var registry =
            new ReplicaConnectionRegistry();

        var broadcaster =
            new ReplicationBroadcaster(
                registry);

        byte[] data =
        [
            1
        ];

        var entry =
            new ReplicationEntry(
                0,
                1,
                data);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await broadcaster.BroadcastAsync(
                    entry,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task BroadcastAsync_WithMultipleReplicas_ShouldWriteSameBytes() {
        var registry =
            new ReplicaConnectionRegistry();

        var fixtures =
            new[]
            {
                CreateConnection("replica-1"),
                CreateConnection("replica-2"),
                CreateConnection("replica-3")
            };

        foreach (ConnectionFixture fixture in fixtures) {
            BringOnline(
                fixture.Connection);

            registry.Register(
                fixture.Connection);
        }

        var broadcaster =
            new ReplicationBroadcaster(
                registry);

        byte[] data =
            "SET counter 100\r\n"u8.ToArray();

        var entry =
            new ReplicationEntry(
                50,
                50 + data.Length,
                data);

        await broadcaster.BroadcastAsync(
            entry);

        foreach (ConnectionFixture fixture in fixtures) {
            Assert.Equal(
                data,
                fixture.Transport.LastWrittenData);
        }
    }

    private static void BringOnline(
        ReplicaConnection connection) {
        connection.MarkConnected();

        connection.BeginHandshake();

        connection.BeginSynchronization();

        connection.MarkOnline();
    }

    private static ConnectionFixture CreateConnection(
        string replicaId) {
        return new ConnectionFixture(
            replicaId);
    }

    private sealed class ConnectionFixture {
        public ConnectionFixture(
            string replicaId) {
            Transport =
                new TestReplicationTransport();

            Connection =
                new ReplicaConnection(
                    new ReplicaInfo(
                        replicaId),
                    Transport);
        }

        public ReplicaConnection Connection { get; }

        public TestReplicationTransport Transport { get; }
    }

    private sealed class TestReplicationTransport :
        IReplicationTransport {
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
            return ValueTask.FromResult(
                0);
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }
}