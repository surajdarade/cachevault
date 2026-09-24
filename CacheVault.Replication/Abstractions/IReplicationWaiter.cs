namespace CacheVault.Replication.Abstractions;

public interface IReplicationWaiter {
    ValueTask<long> WaitAsync(
        int replicaCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}