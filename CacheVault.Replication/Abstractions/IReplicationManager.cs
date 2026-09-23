namespace CacheVault.Replication.Abstractions;

public interface IReplicationManager {
    long ReplicationOffset { get; }

    ValueTask ReplicateAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}