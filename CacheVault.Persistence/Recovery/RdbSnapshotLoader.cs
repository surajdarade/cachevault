using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.Persistence.Recovery;

public sealed class RdbSnapshotLoader
{
    private readonly IKeyValueStore _store;
    private readonly RdbSnapshotReader _reader;
    private readonly IListStore? _listStore;

    public RdbSnapshotLoader(
        IKeyValueStore store,
        RdbSnapshotReader reader,
        IListStore? listStore = null)
    {
        ArgumentNullException.ThrowIfNull(
            store);

        ArgumentNullException.ThrowIfNull(
            reader);

        _store = store;
        _reader = reader;
        _listStore = listStore;
    }

    public int Load(
        Stream stream,
        DateTimeOffset now)
    {
        return LoadWithMetadata(
            stream,
            now,
            replaceExisting: false).RecordsLoaded;
    }

    public RdbLoadResult LoadWithMetadata(
        Stream stream,
        DateTimeOffset now,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        var snapshot =
            _reader.ReadWithMetadata(
                stream,
                now);

        if (replaceExisting)
        {
            if (_store is IResettableKeyValueStore resettableStore)
            {
                resettableStore.Clear();
            }

            if (_listStore is IResettableListStore resettableListStore)
            {
                resettableListStore.Clear();
            }
        }

        int recordsLoaded = 0;

        foreach (var record in snapshot.Records)
        {
            if (record.Type == RdbRecordType.List)
            {
                if (_listStore is null)
                {
                    throw new InvalidOperationException(
                        "The snapshot contains a list but no list store is configured.");
                }

                _store.Remove(record.Key);

                _listStore.Remove(record.Key);

                IReadOnlyList<string> values =
                    record.ListValues ?? [];

                if (values.Count > 0)
                {
                    _listStore.PushRight(
                        record.Key,
                        values);
                }

                recordsLoaded++;
                continue;
            }

            _listStore?.Remove(record.Key);

            _store.Set(
                record.Key,
                record.Value,
                record.ExpiresAt);

            recordsLoaded++;
        }

        return new RdbLoadResult(
            recordsLoaded,
            snapshot.Header.AofOffset);
    }
}
