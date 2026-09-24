using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Persistence.Rdb.Writing;
using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.Master;

public sealed class RdbReplicationSnapshotProvider :
    IReplicationSnapshotProvider {
    private readonly RdbSnapshotWriter _snapshotWriter;

    public RdbReplicationSnapshotProvider(
        IKeyValueStoreSnapshot snapshotStore,
        IListStoreSnapshot? listSnapshotStore = null) {
        ArgumentNullException.ThrowIfNull(
            snapshotStore);

        _snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore,
                listSnapshotStore);
    }

    public async ValueTask WriteSnapshotAsync(
        Stream destination,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            destination);

        cancellationToken.ThrowIfCancellationRequested();

        _snapshotWriter.Write(
            destination,
            DateTimeOffset.UtcNow,
            0);

        await destination.FlushAsync(
            cancellationToken);
    }
}