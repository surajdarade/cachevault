using System.IO;

namespace CacheVault.Replication.Abstractions;

public interface IReplicationSnapshotProvider {
    ValueTask WriteSnapshotAsync(
        Stream destination,
        CancellationToken cancellationToken = default);
}