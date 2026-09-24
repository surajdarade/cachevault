using CacheVault.Replication.Abstractions;

namespace CacheVault.UnitTests.CacheVault.Replication;

public sealed class TestReplicationWaiter : IReplicationWaiter {
    public ValueTask<long> WaitAsync(
        int replicaCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        return ValueTask.FromResult(0L);
    }
}