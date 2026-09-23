using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Core.Storage;
using CacheVault.Persistence.Rdb.Format;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Persistence.Rdb.Writing;
using CacheVault.Persistence.Recovery;

namespace CacheVault.UnitTests.CacheVault.Persistence.Recovery;

public sealed class RdbSnapshotLoaderTests {
    [Fact]
    public void Load_WithEmptySnapshot_ReturnsZero() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(now);

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                now);

        Assert.Equal(
            0,
            restoredCount);
    }

    [Fact]
    public void Load_WithSingleRecord_RestoresValue() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "name",
                    "CacheVault",
                    null));

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                now);

        Assert.Equal(
            1,
            restoredCount);

        Assert.True(
            store.TryGet(
                "name",
                out StoredValue? value));

        Assert.NotNull(
            value);

        Assert.Equal(
            "CacheVault",
            value.Value);
    }

    [Fact]
    public void Load_WithMultipleRecords_RestoresAllValues() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "first",
                    "one",
                    null),
                new RdbRecord(
                    "second",
                    "two",
                    null),
                new RdbRecord(
                    "third",
                    "three",
                    null));

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                now);

        Assert.Equal(
            3,
            restoredCount);

        Assert.Equal(
            "one",
            GetValue(
                store,
                "first"));

        Assert.Equal(
            "two",
            GetValue(
                store,
                "second"));

        Assert.Equal(
            "three",
            GetValue(
                store,
                "third"));
    }

    [Fact]
    public void Load_WithExpiration_RestoresExpiration() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                snapshotTime,
                new RdbRecord(
                    "session",
                    "active",
                    snapshotTime.AddSeconds(30)));

        DateTimeOffset loadTime =
            snapshotTime.AddHours(2);

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                loadTime);

        Assert.Equal(
            1,
            restoredCount);

        Assert.True(
            store.TryGet(
                "session",
                out StoredValue? value));

        Assert.NotNull(
            value);

        Assert.Equal(
            "active",
            value.Value);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            value.ExpiresAt);
    }

    [Fact]
    public void Load_WithMillisecondExpiration_RestoresExpiration() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                snapshotTime,
                new RdbRecord(
                    "temporary",
                    "value",
                    snapshotTime.AddMilliseconds(2500)));

        DateTimeOffset loadTime =
            snapshotTime.AddHours(1);

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        loader.Load(
            stream,
            loadTime);

        Assert.True(
            store.TryGet(
                "temporary",
                out StoredValue? value));

        Assert.NotNull(
            value);

        Assert.Equal(
            snapshotTime.AddMilliseconds(2500),
            value.ExpiresAt);
    }

    [Fact]
    public void Load_WithMixedRecords_RestoresValuesAndExpirations() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                snapshotTime,
                new RdbRecord(
                    "persistent",
                    "value",
                    null),
                new RdbRecord(
                    "seconds",
                    "value",
                    snapshotTime.AddSeconds(30)),
                new RdbRecord(
                    "milliseconds",
                    "value",
                    snapshotTime.AddMilliseconds(2500)));

        DateTimeOffset loadTime =
            snapshotTime.AddHours(1);

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                loadTime);

        Assert.Equal(
            3,
            restoredCount);

        Assert.True(
            store.TryGet(
                "persistent",
                out StoredValue? persistent));

        Assert.NotNull(
            persistent);

        Assert.Null(
            persistent.ExpiresAt);

        Assert.True(
            store.TryGet(
                "seconds",
                out StoredValue? seconds));

        Assert.NotNull(
            seconds);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            seconds.ExpiresAt);

        Assert.True(
            store.TryGet(
                "milliseconds",
                out StoredValue? milliseconds));

        Assert.NotNull(
            milliseconds);

        Assert.Equal(
            snapshotTime.AddMilliseconds(2500),
            milliseconds.ExpiresAt);
    }

    [Fact]
    public void Load_WithUnicodeData_RestoresCorrectly() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "नमस्ते",
                    "CacheVault 🚀",
                    null));

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                now);

        Assert.Equal(
            1,
            restoredCount);

        Assert.Equal(
            "CacheVault 🚀",
            GetValue(
                store,
                "नमस्ते"));
    }

    [Fact]
    public void Load_OverwritesExistingValue() {
        DateTimeOffset now =
            CreateUtcDateTime();

        IKeyValueStore store =
            CreateStore();

        store.Set(
            "name",
            "old-value");

        using MemoryStream stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "name",
                    "new-value",
                    null));

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                now);

        Assert.Equal(
            1,
            restoredCount);

        Assert.Equal(
            "new-value",
            GetValue(
                store,
                "name"));
    }

    [Fact]
    public void Load_DoesNotRemoveKeysMissingFromSnapshot() {
        DateTimeOffset now =
            CreateUtcDateTime();

        IKeyValueStore store =
            CreateStore();

        store.Set(
            "existing",
            "keep");

        using MemoryStream stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "restored",
                    "value",
                    null));

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        loader.Load(
            stream,
            now);

        Assert.Equal(
            "keep",
            GetValue(
                store,
                "existing"));

        Assert.Equal(
            "value",
            GetValue(
                store,
                "restored"));
    }

    [Fact]
    public void Load_WithMalformedSnapshot_DoesNotPartiallyRestoreRecords() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        RdbRecordWriter.WriteRecord(
            writer,
            new RdbRecord(
                "first",
                "value",
                null),
            now);

        writer.WriteByte(
            0x01);

        stream.Position = 0;

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        Assert.Throws<FormatException>(
            () =>
                loader.Load(
                    stream,
                    now));

        Assert.False(
            store.Contains(
                "first"));
    }

    [Fact]
    public void Load_WithMissingEndOfFile_DoesNotRestoreRecords() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        RdbRecordWriter.WriteRecord(
            writer,
            new RdbRecord(
                "key",
                "value",
                null),
            now);

        stream.Position = 0;

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        Assert.Throws<EndOfStreamException>(
            () =>
                loader.Load(
                    stream,
                    now));

        Assert.False(
            store.Contains(
                "key"));
    }

    [Fact]
    public void Load_WithNullStream_ThrowsArgumentNullException() {
        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        Assert.Throws<ArgumentNullException>(
            () =>
                loader.Load(
                    null!,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Constructor_WithNullStore_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () =>
                new RdbSnapshotLoader(
                    null!,
                    new RdbSnapshotReader()));
    }

    [Fact]
    public void Constructor_WithNullReader_ThrowsArgumentNullException() {
        IKeyValueStore store =
            CreateStore();

        Assert.Throws<ArgumentNullException>(
            () =>
                new RdbSnapshotLoader(
                    store,
                    null!));
    }

    [Fact]
    public void Load_ReturnsNumberOfRestoredRecords() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using MemoryStream stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "one",
                    "1",
                    null),
                new RdbRecord(
                    "two",
                    "2",
                    null),
                new RdbRecord(
                    "three",
                    "3",
                    null),
                new RdbRecord(
                    "four",
                    "4",
                    null));

        IKeyValueStore store =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                store,
                new RdbSnapshotReader());

        int restoredCount =
            loader.Load(
                stream,
                now);

        Assert.Equal(
            4,
            restoredCount);
    }

    [Fact]
    public void Load_RoundTripsStoreSnapshotIntoNewStore() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        var sourceStore =
            CreateStore();

        sourceStore.Set(
            "name",
            "Suraj");

        sourceStore.Set(
            "language",
            "CSharp");

        sourceStore.Set(
            "temporary",
            "expires",
            snapshotTime.AddSeconds(30));

        var snapshotStore =
            (IKeyValueStoreSnapshot)sourceStore;

        using var stream =
            new MemoryStream();

        var writer =
            new RdbSnapshotWriter(
                snapshotStore);

        writer.Write(
            stream,
            snapshotTime);

        stream.Position = 0;

        var restoredStore =
            CreateStore();

        var loader =
            new RdbSnapshotLoader(
                restoredStore,
                new RdbSnapshotReader());

        DateTimeOffset loadTime =
            snapshotTime.AddHours(1);

        int restoredCount =
            loader.Load(
                stream,
                loadTime);

        Assert.Equal(
            3,
            restoredCount);

        Assert.Equal(
            "Suraj",
            GetValue(
                restoredStore,
                "name"));

        Assert.Equal(
            "CSharp",
            GetValue(
                restoredStore,
                "language"));

        Assert.True(
            restoredStore.TryGet(
                "temporary",
                out StoredValue? temporary));

        Assert.NotNull(
            temporary);

        Assert.Equal(
            "expires",
            temporary.Value);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            temporary.ExpiresAt);
    }

    private static MemoryStream CreateSnapshotStream(
        DateTimeOffset now,
        params RdbRecord[] records) {
        using var temporaryStream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                temporaryStream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        foreach (RdbRecord record in records) {
            RdbRecordWriter.WriteRecord(
                writer,
                record,
                now);
        }

        writer.WriteByte(
            (byte)RdbOpcode.EndOfFile);

        return new MemoryStream(
            temporaryStream.ToArray());
    }

    private static InMemoryKeyValueStore CreateStore() {
        return new InMemoryKeyValueStore(
            new TestClock(
                CreateUtcDateTime()));
    }

    private static string GetValue(
        IKeyValueStore store,
        string key) {
        Assert.True(
            store.TryGet(
                key,
                out StoredValue? value));

        Assert.NotNull(
            value);

        return value.Value;
    }

    private static DateTimeOffset CreateUtcDateTime() {
        return new DateTimeOffset(
            2026,
            9,
            23,
            10,
            0,
            0,
            TimeSpan.Zero);
    }

    private sealed class TestClock :
        IClock {
        public TestClock(
            DateTimeOffset utcNow) {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}