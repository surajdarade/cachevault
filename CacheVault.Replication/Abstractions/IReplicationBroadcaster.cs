using CacheVault.Replication.State;

namespace CacheVault.Replication.Abstractions;

public interface IReplicationBroadcaster {
    ValueTask BroadcastAsync(
        ReplicationEntry entry,
        CancellationToken cancellationToken = default);
}