using CacheVault.Core.Abstractions;
using CacheVault.Persistence.Rdb.Format;
using CacheVault.Persistence.Rdb.Reading;

namespace CacheVault.Persistence.Recovery;

public sealed class RdbSnapshotLoader {
    private readonly IKeyValueStore _store;
    private readonly RdbSnapshotReader _reader;

    public RdbSnapshotLoader(
        IKeyValueStore store,
        RdbSnapshotReader reader) {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(reader);

        _store = store;
        _reader = reader;
    }

    public int Load(
        Stream stream,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(stream);

        IReadOnlyList<RdbRecord> records =
            _reader.Read(
                stream,
                now);

        foreach (RdbRecord record in records) {
            _store.Set(
                record.Key,
                record.Value,
                record.ExpiresAt);
        }

        return records.Count;
    }
}