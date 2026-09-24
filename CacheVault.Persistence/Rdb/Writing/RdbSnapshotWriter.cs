using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Core.Models;
using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.Persistence.Rdb.Writing;

public sealed class RdbSnapshotWriter
{
    private readonly IKeyValueStoreSnapshot _snapshotStore;
    private readonly IListStoreSnapshot? _listSnapshotStore;

    public RdbSnapshotWriter(
        IKeyValueStoreSnapshot snapshotStore,
        IListStoreSnapshot? listSnapshotStore = null)
    {
        ArgumentNullException.ThrowIfNull(
            snapshotStore);

        _snapshotStore = snapshotStore;
        _listSnapshotStore = listSnapshotStore;
    }

    public void Write(
        Stream stream,
        DateTimeOffset now,
        long aofOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (!stream.CanWrite)
        {
            throw new ArgumentException(
                "The stream must be writable.",
                nameof(stream));
        }

        if (aofOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aofOffset),
                "AOF offset cannot be negative.");
        }

        var writer =
            new RdbBinaryWriter(
                stream);

        var header =
            new RdbHeader(
                RdbHeader.CurrentVersion,
                aofOffset);

        RdbHeaderWriter.WriteHeader(
            writer,
            header);

        IReadOnlyList<PersistentKeyValue> snapshot =
            _snapshotStore.GetSnapshot();

        foreach (PersistentKeyValue entry in snapshot)
        {
            var record =
                new RdbRecord(
                    entry.Key,
                    entry.Value,
                    entry.ExpiresAt);

            RdbRecordWriter.WriteRecord(
                writer,
                record,
                now);
        }

        if (_listSnapshotStore is not null)
        {
            IReadOnlyList<PersistentList> lists =
                _listSnapshotStore.GetSnapshot();

            foreach (PersistentList list in lists)
            {
                RdbRecordWriter.WriteRecord(
                    writer,
                    RdbRecord.CreateList(
                        list.Key,
                        list.Values),
                    now);
            }
        }

        writer.WriteByte(
            (byte)RdbOpcode.EndOfFile);
    }
}
