using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.Persistence.Rdb.Writing;

public sealed class RdbSnapshotWriter {
    private readonly IKeyValueStoreSnapshot _snapshotStore;

    public RdbSnapshotWriter(
        IKeyValueStoreSnapshot snapshotStore) {
        ArgumentNullException.ThrowIfNull(
            snapshotStore);

        _snapshotStore = snapshotStore;
    }

    public void Write(
        Stream stream,
        DateTimeOffset now,
        long aofOffset = 0) {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (!stream.CanWrite) {
            throw new ArgumentException(
                "The stream must be writable.",
                nameof(stream));
        }

        if (aofOffset < 0) {
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

        foreach (PersistentKeyValue entry in snapshot) {
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

        writer.WriteByte(
            (byte)RdbOpcode.EndOfFile);
    }
}