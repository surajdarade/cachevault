using CacheVault.Replication.Master;
using CacheVault.Replication.State;
using CacheVault.Replication.Abstractions;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class ReplicaConnectionRegistryTests {
    [Fact]
    public void Constructor_ShouldStartEmpty() {
        var registry =
            new ReplicaConnectionRegistry();

        Assert.Equal(
            0,
            registry.Count);

        Assert.Empty(
            registry.GetAll());
    }

    [Fact]
    public void Register_ShouldAddConnection() {
        var registry =
            new ReplicaConnectionRegistry();

        var connection =
            CreateConnection(
                "replica-1");

        registry.Register(
            connection);

        Assert.Equal(
            1,
            registry.Count);

        Assert.Contains(
            connection,
            registry.GetAll());
    }

    [Fact]
    public void Register_DuplicateReplica_ShouldThrow() {
        var registry =
            new ReplicaConnectionRegistry();

        registry.Register(
            CreateConnection(
                "replica-1"));

        Assert.Throws<InvalidOperationException>(
            () =>
                registry.Register(
                    CreateConnection(
                        "replica-1")));
    }

    [Fact]
    public void TryGet_ExistingReplica_ShouldReturnConnection() {
        var registry =
            new ReplicaConnectionRegistry();

        var connection =
            CreateConnection(
                "replica-1");

        registry.Register(
            connection);

        bool found =
            registry.TryGet(
                "replica-1",
                out ReplicaConnection? result);

        Assert.True(
            found);

        Assert.Same(
            connection,
            result);
    }

    [Fact]
    public void TryGet_MissingReplica_ShouldReturnFalse() {
        var registry =
            new ReplicaConnectionRegistry();

        bool found =
            registry.TryGet(
                "missing",
                out ReplicaConnection? result);

        Assert.False(
            found);

        Assert.Null(
            result);
    }

    [Fact]
    public void Remove_ExistingReplica_ShouldRemoveConnection() {
        var registry =
            new ReplicaConnectionRegistry();

        registry.Register(
            CreateConnection(
                "replica-1"));

        bool removed =
            registry.Remove(
                "replica-1");

        Assert.True(
            removed);

        Assert.Equal(
            0,
            registry.Count);
    }

    [Fact]
    public void Remove_MissingReplica_ShouldReturnFalse() {
        var registry =
            new ReplicaConnectionRegistry();

        bool removed =
            registry.Remove(
                "missing");

        Assert.False(
            removed);
    }

    [Fact]
    public void GetAll_ShouldReturnAllConnections() {
        var registry =
            new ReplicaConnectionRegistry();

        var replica1 =
            CreateConnection(
                "replica-1");

        var replica2 =
            CreateConnection(
                "replica-2");

        registry.Register(
            replica1);

        registry.Register(
            replica2);

        IReadOnlyList<ReplicaConnection> connections =
            registry.GetAll();

        Assert.Equal(
            2,
            connections.Count);

        Assert.Contains(
            replica1,
            connections);

        Assert.Contains(
            replica2,
            connections);
    }

    [Fact]
    public void Remove_ShouldNotRemoveOtherReplicas() {
        var registry =
            new ReplicaConnectionRegistry();

        var replica1 =
            CreateConnection(
                "replica-1");

        var replica2 =
            CreateConnection(
                "replica-2");

        registry.Register(
            replica1);

        registry.Register(
            replica2);

        registry.Remove(
            "replica-1");

        Assert.False(
            registry.TryGet(
                "replica-1",
                out _));

        Assert.True(
            registry.TryGet(
                "replica-2",
                out ReplicaConnection? result));

        Assert.Same(
            replica2,
            result);
    }

    private static ReplicaConnection CreateConnection(
        string replicaId) {
        return new ReplicaConnection(
            new ReplicaInfo(
                replicaId),
            new TestReplicationTransport());
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
            return ValueTask.FromResult(
                0);
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }
}