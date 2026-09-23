using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Persistence.Rdb.Format;
using CacheVault.Persistence.Rdb.Writing;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Writing;

public sealed class RdbSnapshotWriterTests {
    [Fact]
    public void Write_WithEmptySnapshot_WritesHeaderAndEndOfFile() {
        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        DateTimeOffset now =
            CreateUtcDateTime();

        snapshotWriter.Write(
            stream,
            now);

        byte[] bytes =
            stream.ToArray();

        using var readStream =
            new MemoryStream(bytes);

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                readStream);

        Assert.Equal(
            RdbHeader.CurrentVersion,
            header.Version);

        Assert.Equal(
            (byte)RdbOpcode.EndOfFile,
            readStream.ReadByte());

        Assert.Equal(
            readStream.Length,
            readStream.Position);
    }

    [Fact]
    public void Write_WithSingleRecord_WritesHeaderRecordAndEndOfFile() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshot =
            new[]
            {
                new PersistentKeyValue(
                    "name",
                    "CacheVault")
            };

        var snapshotStore =
            new TestSnapshotStore(
                snapshot);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            now);

        stream.Position = 0;

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            RdbHeader.CurrentVersion,
            header.Version);

        RdbRecord record =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "name",
            record.Key);

        Assert.Equal(
            "CacheVault",
            record.Value);

        Assert.Null(
            record.ExpiresAt);

        Assert.Equal(
            (byte)RdbOpcode.EndOfFile,
            stream.ReadByte());

        Assert.Equal(
            stream.Length,
            stream.Position);
    }

    [Fact]
    public void Write_WithMultipleRecords_WritesAllRecordsInSnapshot() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshot =
            new[]
            {
                new PersistentKeyValue(
                    "first",
                    "one"),

                new PersistentKeyValue(
                    "second",
                    "two"),

                new PersistentKeyValue(
                    "third",
                    "three")
            };

        var snapshotStore =
            new TestSnapshotStore(
                snapshot);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            now);

        stream.Position = 0;

        RdbHeaderReader.ReadHeader(
            stream);

        RdbRecord first =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        RdbRecord second =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        RdbRecord third =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "first",
            first.Key);

        Assert.Equal(
            "one",
            first.Value);

        Assert.Equal(
            "second",
            second.Key);

        Assert.Equal(
            "two",
            second.Value);

        Assert.Equal(
            "third",
            third.Key);

        Assert.Equal(
            "three",
            third.Value);

        Assert.Equal(
            (byte)RdbOpcode.EndOfFile,
            stream.ReadByte());

        Assert.Equal(
            stream.Length,
            stream.Position);
    }

    [Fact]
    public void Write_WithExpiration_PreservesAbsoluteExpiration() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshot =
            new[]
            {
                new PersistentKeyValue(
                    "session",
                    "active",
                    now.AddSeconds(30))
            };

        var snapshotStore =
            new TestSnapshotStore(
                snapshot);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            now);

        stream.Position = 0;

        RdbHeaderReader.ReadHeader(
            stream);

        DateTimeOffset loadTime =
            now.AddHours(2);

        RdbRecord record =
            RdbRecordReader.ReadRecord(
                stream,
                loadTime);

        Assert.Equal(
            "session",
            record.Key);

        Assert.Equal(
            "active",
            record.Value);

        Assert.Equal(
            now.AddSeconds(30),
            record.ExpiresAt);

        Assert.Equal(
            (byte)RdbOpcode.EndOfFile,
            stream.ReadByte());
    }

    [Fact]
    public void Write_WithMillisecondExpiration_PreservesAbsoluteExpiration() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshot =
            new[]
            {
                new PersistentKeyValue(
                    "temporary",
                    "value",
                    now.AddMilliseconds(2500))
            };

        var snapshotStore =
            new TestSnapshotStore(
                snapshot);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            now);

        stream.Position = 0;

        RdbHeaderReader.ReadHeader(
            stream);

        DateTimeOffset loadTime =
            now.AddHours(1);

        RdbRecord record =
            RdbRecordReader.ReadRecord(
                stream,
                loadTime);

        Assert.Equal(
            now.AddMilliseconds(2500),
            record.ExpiresAt);
    }

    [Fact]
    public void Write_PreservesUnicodeKeysAndValues() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshot =
            new[]
            {
                new PersistentKeyValue(
                    "नमस्ते",
                    "CacheVault 🚀")
            };

        var snapshotStore =
            new TestSnapshotStore(
                snapshot);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            now);

        stream.Position = 0;

        RdbHeaderReader.ReadHeader(
            stream);

        RdbRecord record =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "नमस्ते",
            record.Key);

        Assert.Equal(
            "CacheVault 🚀",
            record.Value);
    }

    [Fact]
    public void Write_UsesCurrentRdbVersion() {
        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            CreateUtcDateTime());

        stream.Position = 0;

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            RdbHeader.Magic,
            "CVDB");

        Assert.Equal(
            RdbHeader.CurrentVersion,
            header.Version);
    }

    [Fact]
    public void Write_WithDefaultAofOffset_WritesZeroAofOffset() {
        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            CreateUtcDateTime());

        stream.Position = 0;

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            0,
            header.AofOffset);
    }

    [Fact]
    public void Write_WithAofOffset_WritesAofOffsetToHeader() {
        const long expectedAofOffset =
            123456789;

        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            CreateUtcDateTime(),
            expectedAofOffset);

        stream.Position = 0;

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            expectedAofOffset,
            header.AofOffset);
    }

    [Fact]
    public void Write_WithNegativeAofOffset_ThrowsArgumentOutOfRangeException() {
        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                snapshotWriter.Write(
                    stream,
                    CreateUtcDateTime(),
                    -1));
    }

    [Fact]
    public void Write_WritesEndOfFileAsFinalByte() {
        var snapshotStore =
            new TestSnapshotStore(
                [
                    new PersistentKeyValue(
                        "key",
                        "value")
                ]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            CreateUtcDateTime());

        byte[] bytes =
            stream.ToArray();

        Assert.NotEmpty(
            bytes);

        Assert.Equal(
            (byte)RdbOpcode.EndOfFile,
            bytes[^1]);
    }

    [Fact]
    public void Write_RequestsSnapshotExactlyOnce() {
        var snapshotStore =
            new TestSnapshotStore(
                [
                    new PersistentKeyValue(
                        "key",
                        "value")
                ]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            CreateUtcDateTime());

        Assert.Equal(
            1,
            snapshotStore.GetSnapshotCallCount);
    }

    [Fact]
    public void Write_UsesSnapshotReturnedAtWriteTime() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var initialSnapshot =
            new[]
            {
                new PersistentKeyValue(
                    "initial",
                    "value")
            };

        var snapshotStore =
            new TestSnapshotStore(
                initialSnapshot);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new MemoryStream();

        snapshotWriter.Write(
            stream,
            now);

        snapshotStore.SetSnapshot(
            [
                new PersistentKeyValue(
                    "different",
                    "value")
            ]);

        stream.Position = 0;

        RdbHeaderReader.ReadHeader(
            stream);

        RdbRecord record =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "initial",
            record.Key);

        Assert.Equal(
            "value",
            record.Value);
    }

    [Fact]
    public void Write_WithNonWritableStream_ThrowsArgumentException() {
        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        using var stream =
            new ReadOnlyTestStream();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    snapshotWriter.Write(
                        stream,
                        CreateUtcDateTime()));

        Assert.Contains(
            "writable",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_WithNullStream_ThrowsArgumentNullException() {
        var snapshotStore =
            new TestSnapshotStore([]);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        Assert.Throws<ArgumentNullException>(
            () =>
                snapshotWriter.Write(
                    null!,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Constructor_WithNullSnapshotStore_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () =>
                new RdbSnapshotWriter(
                    null!));
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

    private sealed class TestSnapshotStore :
        IKeyValueStoreSnapshot {
        private IReadOnlyList<PersistentKeyValue> _snapshot;

        public TestSnapshotStore(
            IReadOnlyList<PersistentKeyValue> snapshot) {
            _snapshot = snapshot;
        }

        public int GetSnapshotCallCount { get; private set; }

        public IReadOnlyList<PersistentKeyValue> GetSnapshot() {
            GetSnapshotCallCount++;

            return _snapshot;
        }

        public void SetSnapshot(
            IReadOnlyList<PersistentKeyValue> snapshot) {
            _snapshot = snapshot;
        }
    }

    private sealed class ReadOnlyTestStream :
        MemoryStream {
        public override bool CanWrite =>
            false;

        public override void Write(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }

        public override void Write(
            ReadOnlySpan<byte> buffer) {
            throw new NotSupportedException();
        }

        public override void WriteByte(
            byte value) {
            throw new NotSupportedException();
        }
    }
}