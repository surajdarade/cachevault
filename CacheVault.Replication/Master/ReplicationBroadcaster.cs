using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicationBroadcaster :
    IReplicationBroadcaster {
    private readonly IReplicaConnectionRegistry
        _connectionRegistry;

    public ReplicationBroadcaster(
        IReplicaConnectionRegistry connectionRegistry) {
        ArgumentNullException.ThrowIfNull(
            connectionRegistry);

        _connectionRegistry =
            connectionRegistry;
    }

    public async ValueTask BroadcastAsync(
        ReplicationEntry entry,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            entry);

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ReplicaConnection> connections =
            _connectionRegistry.GetAll();

        List<Task> writeTasks = [];

        foreach (ReplicaConnection connection in connections) {
            if (connection.State !=
                ReplicaConnectionState.Online) {
                continue;
            }

            writeTasks.Add(
                WriteToReplicaAsync(
                    connection,
                    entry.Data,
                    cancellationToken));
        }

        if (writeTasks.Count == 0) {
            return;
        }

        await Task.WhenAll(
            writeTasks);
    }

    private static async Task WriteToReplicaAsync(
        ReplicaConnection connection,
        byte[] data,
        CancellationToken cancellationToken) {
        await connection.WriteAsync(
            data,
            cancellationToken);
    }
}