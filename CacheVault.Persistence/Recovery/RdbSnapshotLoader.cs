using CacheVault.Core.Abstractions;
using CacheVault.Persistence.Rdb.Reading;

namespace CacheVault.Persistence.Recovery;

public sealed class RdbSnapshotLoader {
    private readonly IKeyValueStore _store;
    private readonly RdbSnapshotReader _reader;

    public RdbSnapshotLoader(
        IKeyValueStore store,
        RdbSnapshotReader reader) {
        ArgumentNullException.ThrowIfNull(
            store);

        ArgumentNullException.ThrowIfNull(
            reader);

        _store = store;
        _reader = reader;
    }

    public int Load(
        Stream stream,
        DateTimeOffset now) {
        RdbLoadResult result =
            LoadWithMetadata(
                stream,
                now);

        return result.RecordsLoaded;
    }

    public RdbLoadResult LoadWithMetadata(
        Stream stream,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(
            stream);

        var snapshot =
            _reader.ReadWithMetadata(
                stream,
                now);

        foreach (var record in snapshot.Records) {
            _store.Set(
                record.Key,
                record.Value,
                record.ExpiresAt);
        }

        return new RdbLoadResult(
            snapshot.Records.Count,
            snapshot.Header.AofOffset);
    }
}