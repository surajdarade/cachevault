using CacheVault.Persistence.Rdb.Format;
using CacheVault.Persistence.Rdb.Reading;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Reading;

public sealed class RdbSnapshotReaderTests {
    [Fact]
    public void Read_WithEmptySnapshot_ReturnsEmptyCollection() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            CreateSnapshotStream(
                now);

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                now);

        Assert.Empty(
            records);
    }

    [Fact]
    public void Read_WithSingleRecord_ReturnsRecord() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "name",
                    "CacheVault",
                    null));

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                now);

        RdbRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            "name",
            record.Key);

        Assert.Equal(
            "CacheVault",
            record.Value);

        Assert.Null(
            record.ExpiresAt);
    }

    [Fact]
    public void Read_WithMultipleRecords_ReturnsAllRecords() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
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

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                now);

        Assert.Equal(
            3,
            records.Count);

        Assert.Equal(
            "first",
            records[0].Key);

        Assert.Equal(
            "one",
            records[0].Value);

        Assert.Equal(
            "second",
            records[1].Key);

        Assert.Equal(
            "two",
            records[1].Value);

        Assert.Equal(
            "third",
            records[2].Key);

        Assert.Equal(
            "three",
            records[2].Value);
    }

    [Fact]
    public void Read_WithSecondExpiration_PreservesAbsoluteExpiration() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        using var stream =
            CreateSnapshotStream(
                snapshotTime,
                new RdbRecord(
                    "session",
                    "active",
                    snapshotTime.AddSeconds(30)));

        DateTimeOffset loadTime =
            snapshotTime.AddHours(2);

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                loadTime);

        RdbRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            record.ExpiresAt);
    }

    [Fact]
    public void Read_WithMillisecondExpiration_PreservesAbsoluteExpiration() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        using var stream =
            CreateSnapshotStream(
                snapshotTime,
                new RdbRecord(
                    "temporary",
                    "value",
                    snapshotTime.AddMilliseconds(2500)));

        DateTimeOffset loadTime =
            snapshotTime.AddHours(2);

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                loadTime);

        RdbRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            snapshotTime.AddMilliseconds(2500),
            record.ExpiresAt);
    }

    [Fact]
    public void Read_WithMixedExpirationRecords_ReadsAllRecords() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        using var stream =
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

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                loadTime);

        Assert.Equal(
            3,
            records.Count);

        Assert.Null(
            records[0].ExpiresAt);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            records[1].ExpiresAt);

        Assert.Equal(
            snapshotTime.AddMilliseconds(2500),
            records[2].ExpiresAt);
    }

    [Fact]
    public void Read_WithUnicodeData_ReadsCorrectly() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    "नमस्ते",
                    "CacheVault 🚀",
                    null));

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                now);

        RdbRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            "नमस्ते",
            record.Key);

        Assert.Equal(
            "CacheVault 🚀",
            record.Value);
    }

    [Fact]
    public void Read_WithEmptyKeyAndValue_ReadsCorrectly() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            CreateSnapshotStream(
                now,
                new RdbRecord(
                    string.Empty,
                    string.Empty,
                    null));

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                now);

        RdbRecord record =
            Assert.Single(
                records);

        Assert.Equal(
            string.Empty,
            record.Key);

        Assert.Equal(
            string.Empty,
            record.Value);
    }

    [Fact]
    public void Read_WithMissingEndOfFile_ThrowsEndOfStreamException() {
        DateTimeOffset now =
            CreateUtcDateTime();

        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<EndOfStreamException>(
            () =>
                reader.Read(
                    stream,
                    now));
    }

    [Fact]
    public void Read_WithInvalidMagic_ThrowsFormatException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        writer.WriteBytes(
            "XXXX"u8);

        writer.WriteUInt16(
            RdbHeader.CurrentVersion);

        writer.WriteByte(
            (byte)RdbOpcode.EndOfFile);

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<FormatException>(
            () =>
                reader.Read(
                    stream,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Read_WithUnsupportedVersion_ThrowsFormatException() {
        using var stream =
            new MemoryStream(
            [
                (byte)'C',
            (byte)'V',
            (byte)'D',
            (byte)'B',

            // Unsupported RDB version.
            0x02,
            0x00,

            // AOF offset = 0.
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00
            ]);

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<FormatException>(
            () =>
                reader.Read(
                    stream,
                    DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Read_WithUnsupportedRecordOpcode_ThrowsFormatException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        writer.WriteByte(
            0x01);

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        FormatException exception =
            Assert.Throws<FormatException>(
                () =>
                    reader.Read(
                        stream,
                        CreateUtcDateTime()));

        Assert.Contains(
            "Unsupported RDB record opcode",
            exception.Message);
    }

    [Fact]
    public void Read_WithTruncatedExpiration_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        writer.WriteByte(
            (byte)RdbOpcode.ExpireSeconds);

        writer.WriteByte(
            0x01);

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<EndOfStreamException>(
            () =>
                reader.Read(
                    stream,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Read_WithExpirationButMissingStringOpcode_ThrowsFormatException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        writer.WriteByte(
            (byte)RdbOpcode.ExpireSeconds);

        writer.WriteInt64(
            30);

        writer.WriteByte(
            0x01);

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        FormatException exception =
            Assert.Throws<FormatException>(
                () =>
                    reader.Read(
                        stream,
                        CreateUtcDateTime()));

        Assert.Contains(
            "Unsupported RDB record opcode",
            exception.Message);
    }

    [Fact]
    public void Read_WithTruncatedKey_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        writer.WriteByte(
            (byte)RdbOpcode.StringValue);

        RdbLengthEncoder.WriteLength(
            writer,
            5);

        writer.WriteBytes(
            "abc"u8);

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<EndOfStreamException>(
            () =>
                reader.Read(
                    stream,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Read_WithTruncatedValue_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbHeaderWriter.WriteHeader(
            writer,
            new RdbHeader(
                RdbHeader.CurrentVersion));

        writer.WriteByte(
            (byte)RdbOpcode.StringValue);

        RdbStringEncoder.WriteString(
            writer,
            "key");

        RdbLengthEncoder.WriteLength(
            writer,
            5);

        writer.WriteBytes(
            "abc"u8);

        stream.Position = 0;

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<EndOfStreamException>(
            () =>
                reader.Read(
                    stream,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Read_WithEmptyStream_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        var reader =
            new RdbSnapshotReader();

        Assert.Throws<EndOfStreamException>(
            () =>
                reader.Read(
                    stream,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Read_WithNullStream_ThrowsArgumentNullException() {
        var reader =
            new RdbSnapshotReader();

        Assert.Throws<ArgumentNullException>(
            () =>
                reader.Read(
                    null!,
                    CreateUtcDateTime()));
    }

    [Fact]
    public void Read_WithUnreadableStream_ThrowsArgumentException() {
        var reader =
            new RdbSnapshotReader();

        using var stream =
            new WriteOnlyTestStream();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    reader.Read(
                        stream,
                        CreateUtcDateTime()));

        Assert.Contains(
            "readable",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_RoundTripsSnapshotWrittenByWriter() {
        DateTimeOffset snapshotTime =
            CreateUtcDateTime();

        var originalRecords =
            new[]
            {
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
                    snapshotTime.AddMilliseconds(2500))
            };

        using var stream =
            new MemoryStream();

        var writer =
            new RdbSnapshotWriterTestHelper();

        writer.Write(
            stream,
            originalRecords,
            snapshotTime);

        stream.Position = 0;

        DateTimeOffset loadTime =
            snapshotTime.AddHours(1);

        var reader =
            new RdbSnapshotReader();

        IReadOnlyList<RdbRecord> records =
            reader.Read(
                stream,
                loadTime);

        Assert.Equal(
            3,
            records.Count);

        Assert.Equal(
            originalRecords[0].Key,
            records[0].Key);

        Assert.Equal(
            originalRecords[0].Value,
            records[0].Value);

        Assert.Null(
            records[0].ExpiresAt);

        Assert.Equal(
            originalRecords[1].Key,
            records[1].Key);

        Assert.Equal(
            originalRecords[1].Value,
            records[1].Value);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            records[1].ExpiresAt);

        Assert.Equal(
            originalRecords[2].Key,
            records[2].Key);

        Assert.Equal(
            originalRecords[2].Value,
            records[2].Value);

        Assert.Equal(
            snapshotTime.AddMilliseconds(2500),
            records[2].ExpiresAt);
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

    private sealed class WriteOnlyTestStream :
        MemoryStream {
        public override bool CanRead =>
            false;

        public override int Read(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }

        public override int Read(
            Span<byte> buffer) {
            throw new NotSupportedException();
        }
    }

    private sealed class RdbSnapshotWriterTestHelper {
        public void Write(
            Stream stream,
            IReadOnlyList<RdbRecord> records,
            DateTimeOffset now) {
            var writer =
                new RdbBinaryWriter(
                    stream);

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
        }
    }
}