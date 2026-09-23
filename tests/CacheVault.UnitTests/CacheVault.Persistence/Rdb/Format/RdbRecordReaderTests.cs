using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbRecordReaderTests {
    [Fact]
    public void ReadRecord_WithoutExpiration_ReadsKeyAndValue() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        var record =
            new RdbRecord(
                "name",
                "CacheVault",
                null);

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        stream.Position = 0;

        RdbRecord result =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "name",
            result.Key);

        Assert.Equal(
            "CacheVault",
            result.Value);

        Assert.Null(
            result.ExpiresAt);
    }

    [Fact]
    public void ReadRecord_WithMillisecondExpiration_PreservesAbsoluteExpiration() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var record =
            new RdbRecord(
                "key",
                "value",
                now.AddMilliseconds(2500));

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        stream.Position = 0;

        DateTimeOffset loadTime =
            now.AddMinutes(5);

        RdbRecord result =
            RdbRecordReader.ReadRecord(
                stream,
                loadTime);

        Assert.Equal(
            "key",
            result.Key);

        Assert.Equal(
            "value",
            result.Value);

        Assert.Equal(
            now.AddMilliseconds(2500),
            result.ExpiresAt);
    }

    [Fact]
    public void ReadRecord_WithSecondExpiration_PreservesAbsoluteExpiration() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset snapshotTime =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var record =
            new RdbRecord(
                "session",
                "active",
                snapshotTime.AddSeconds(30));

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            snapshotTime);

        stream.Position = 0;

        DateTimeOffset loadTime =
            snapshotTime.AddHours(2);

        RdbRecord result =
            RdbRecordReader.ReadRecord(
                stream,
                loadTime);

        Assert.Equal(
            snapshotTime.AddSeconds(30),
            result.ExpiresAt);
    }

    [Fact]
    public void ReadRecord_WithUnicodeKeyAndValue_ReadsCorrectly() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var record =
            new RdbRecord(
                "नमस्ते",
                "CacheVault 🚀",
                null);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        stream.Position = 0;

        RdbRecord result =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "नमस्ते",
            result.Key);

        Assert.Equal(
            "CacheVault 🚀",
            result.Value);
    }

    [Fact]
    public void ReadRecord_WithEmptyKeyAndValue_ReadsCorrectly() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var record =
            new RdbRecord(
                string.Empty,
                string.Empty,
                null);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        stream.Position = 0;

        RdbRecord result =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            string.Empty,
            result.Key);

        Assert.Equal(
            string.Empty,
            result.Value);
    }

    [Fact]
    public void ReadRecord_WithMultipleRecords_ReadsSequentially() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var firstRecord =
            new RdbRecord(
                "first",
                "one",
                null);

        var secondRecord =
            new RdbRecord(
                "second",
                "two",
                now.AddSeconds(10));

        RdbRecordWriter.WriteRecord(
            writer,
            firstRecord,
            now);

        RdbRecordWriter.WriteRecord(
            writer,
            secondRecord,
            now);

        stream.Position = 0;

        RdbRecord firstResult =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        RdbRecord secondResult =
            RdbRecordReader.ReadRecord(
                stream,
                now);

        Assert.Equal(
            "first",
            firstResult.Key);

        Assert.Equal(
            "one",
            firstResult.Value);

        Assert.Null(
            firstResult.ExpiresAt);

        Assert.Equal(
            "second",
            secondResult.Key);

        Assert.Equal(
            "two",
            secondResult.Value);

        Assert.Equal(
            now.AddSeconds(10),
            secondResult.ExpiresAt);
    }

    [Fact]
    public void ReadRecord_WithUnsupportedOpcode_ThrowsFormatException() {
        using var stream = new MemoryStream();

        stream.WriteByte(0x01);

        stream.Position = 0;

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        FormatException exception =
            Assert.Throws<FormatException>(
                () =>
                    RdbRecordReader.ReadRecord(
                        stream,
                        now));

        Assert.Contains(
            "Unsupported RDB record opcode",
            exception.Message);
    }

    [Fact]
    public void ReadRecord_WithEmptyStream_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbRecordReader.ReadRecord(
                    stream,
                    now));
    }

    [Fact]
    public void ReadRecord_WithTruncatedMillisecondExpiration_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        stream.WriteByte(
            (byte)RdbOpcode.ExpireMilliseconds);

        stream.WriteByte(0x01);

        stream.Position = 0;

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbRecordReader.ReadRecord(
                    stream,
                    now));
    }

    [Fact]
    public void ReadRecord_WithTruncatedSecondsExpiration_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        stream.WriteByte(
            (byte)RdbOpcode.ExpireSeconds);

        stream.WriteByte(0x01);

        stream.Position = 0;

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbRecordReader.ReadRecord(
                    stream,
                    now));
    }

    [Fact]
    public void ReadRecord_WithExpirationButMissingStringOpcode_ThrowsFormatException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        writer.WriteByte(
            (byte)RdbOpcode.ExpireSeconds);

        writer.WriteInt64(10);

        writer.WriteByte(
            0x01);

        stream.Position = 0;

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        FormatException exception =
            Assert.Throws<FormatException>(
                () =>
                    RdbRecordReader.ReadRecord(
                        stream,
                        now));

        Assert.Contains(
            "Unsupported RDB record opcode",
            exception.Message);
    }

    [Fact]
    public void ReadRecord_WithTruncatedKey_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        writer.WriteByte(
            (byte)RdbOpcode.StringValue);

        RdbLengthEncoder.WriteLength(
            writer,
            5);

        writer.WriteBytes(
            "abc"u8);

        stream.Position = 0;

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbRecordReader.ReadRecord(
                    stream,
                    now));
    }

    [Fact]
    public void ReadRecord_WithTruncatedValue_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

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

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbRecordReader.ReadRecord(
                    stream,
                    now));
    }

    [Fact]
    public void ReadRecord_WithNullStream_ThrowsArgumentNullException() {
        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        Assert.Throws<ArgumentNullException>(
            () =>
                RdbRecordReader.ReadRecord(
                    null!,
                    now));
    }

    [Fact]
    public void ReadRecord_RoundTripsRecordWithoutExpiration() {
        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var original =
            new RdbRecord(
                "user:100",
                "Suraj",
                null);

        RdbRecord result =
            RoundTrip(
                original,
                now,
                now);

        Assert.Equal(
            original.Key,
            result.Key);

        Assert.Equal(
            original.Value,
            result.Value);

        Assert.Equal(
            original.ExpiresAt,
            result.ExpiresAt);
    }

    [Fact]
    public void ReadRecord_RoundTripsRecordWithMillisecondExpiration() {
        DateTimeOffset snapshotTime =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        DateTimeOffset now =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var original =
            new RdbRecord(
                "cache:key",
                "hello",
                snapshotTime.AddMilliseconds(2500));

        DateTimeOffset loadTime =
            snapshotTime.AddMinutes(10);

        RdbRecord result =
            RoundTrip(
                original,
                snapshotTime,
                loadTime);

        Assert.Equal(
            original.Key,
            result.Key);

        Assert.Equal(
            original.Value,
            result.Value);

        Assert.Equal(
            now.AddMilliseconds(2500),
            result.ExpiresAt);
    }

    [Fact]
    public void ReadRecord_RoundTripsRecordWithSecondExpiration() {
        DateTimeOffset snapshotTime =
            new(
                2026,
                9,
                23,
                10,
                0,
                0,
                TimeSpan.Zero);

        var original =
            new RdbRecord(
                "session:42",
                "active",
                snapshotTime.AddSeconds(60));

        DateTimeOffset loadTime =
            snapshotTime.AddHours(3);

        RdbRecord result =
            RoundTrip(
                original,
                snapshotTime,
                loadTime);

        Assert.Equal(
            original.Key,
            result.Key);

        Assert.Equal(
            original.Value,
            result.Value);

        Assert.Equal(
            original.ExpiresAt,
            result.ExpiresAt);
    }

    private static RdbRecord RoundTrip(
        RdbRecord record,
        DateTimeOffset snapshotTime,
        DateTimeOffset loadTime) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            snapshotTime);

        stream.Position = 0;

        return RdbRecordReader.ReadRecord(
            stream,
            loadTime);
    }
}