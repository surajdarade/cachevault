using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbRecordWriterTests {
    [Fact]
    public void WriteRecord_WithoutExpiration_WritesStringOpcodeAndKeyValue() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        var record =
            new RdbRecord(
                "name",
                "CacheVault",
                null);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        byte[] bytes =
            stream.ToArray();

        Assert.Equal(
            (byte)RdbOpcode.StringValue,
            bytes[0]);

        using var readStream =
            new MemoryStream(bytes[1..]);

        Assert.Equal(
            "name",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            "CacheVault",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            readStream.Length,
            readStream.Position);
    }

    [Fact]
    public void WriteRecord_WithMillisecondExpiration_WritesMillisecondOpcodeAndTtl() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset expiresAt =
            now.AddMilliseconds(2500);

        var record =
            new RdbRecord(
                "key",
                "value",
                expiresAt);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        byte[] bytes =
            stream.ToArray();

        Assert.Equal(
            (byte)RdbOpcode.ExpireMilliseconds,
            bytes[0]);

        using var readStream =
            new MemoryStream(bytes[1..]);

        var binaryReader =
            new BinaryReader(
                readStream);

        long ttl =
            binaryReader.ReadInt64();

        Assert.Equal(
            2500,
            ttl);

        Assert.Equal(
            (byte)RdbOpcode.StringValue,
            binaryReader.ReadByte());

        Assert.Equal(
            "key",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            "value",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            readStream.Length,
            readStream.Position);
    }

    [Fact]
    public void WriteRecord_WithSecondAlignedExpiration_WritesSecondOpcodeAndTtl() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset expiresAt =
            now.AddSeconds(30);

        var record =
            new RdbRecord(
                "session",
                "active",
                expiresAt);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        byte[] bytes =
            stream.ToArray();

        Assert.Equal(
            (byte)RdbOpcode.ExpireSeconds,
            bytes[0]);

        using var readStream =
            new MemoryStream(bytes[1..]);

        var binaryReader =
            new BinaryReader(
                readStream);

        long ttl =
            binaryReader.ReadInt64();

        Assert.Equal(
            30,
            ttl);

        Assert.Equal(
            (byte)RdbOpcode.StringValue,
            binaryReader.ReadByte());

        Assert.Equal(
            "session",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            "active",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            readStream.Length,
            readStream.Position);
    }

    [Fact]
    public void WriteRecord_WithExactlyOneSecondExpiration_UsesSeconds() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        var record =
            new RdbRecord(
                "key",
                "value",
                now.AddSeconds(1));

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        byte[] bytes =
            stream.ToArray();

        Assert.Equal(
            (byte)RdbOpcode.ExpireSeconds,
            bytes[0]);
    }

    [Fact]
    public void WriteRecord_WithNonAlignedExpiration_UsesMilliseconds() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        var record =
            new RdbRecord(
                "key",
                "value",
                now.AddMilliseconds(1001));

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        byte[] bytes =
            stream.ToArray();

        Assert.Equal(
            (byte)RdbOpcode.ExpireMilliseconds,
            bytes[0]);
    }

    [Fact]
    public void WriteRecord_WithExpiredRecord_Throws() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        var record =
            new RdbRecord(
                "key",
                "value",
                now.AddMilliseconds(-1));

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    RdbRecordWriter.WriteRecord(
                        writer,
                        record,
                        now));

        Assert.Contains(
            "expired",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteRecord_WithExpirationExactlyAtNow_Throws() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        var record =
            new RdbRecord(
                "key",
                "value",
                now);

        Assert.Throws<ArgumentException>(
            () =>
                RdbRecordWriter.WriteRecord(
                    writer,
                    record,
                    now));
    }

    [Fact]
    public void WriteRecord_PreservesUnicodeKeyAndValue() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        var record =
            new RdbRecord(
                "नमस्ते",
                "CacheVault 🚀",
                null);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        using var readStream =
            new MemoryStream(
                stream.ToArray()[1..]);

        Assert.Equal(
            "नमस्ते",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            "CacheVault 🚀",
            RdbStringDecoder.ReadString(readStream));
    }

    [Fact]
    public void WriteRecord_WithEmptyKeyAndValue_WritesSuccessfully() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        var record =
            new RdbRecord(
                string.Empty,
                string.Empty,
                null);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        RdbRecordWriter.WriteRecord(
            writer,
            record,
            now);

        using var readStream =
            new MemoryStream(
                stream.ToArray()[1..]);

        Assert.Equal(
            string.Empty,
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            string.Empty,
            RdbStringDecoder.ReadString(readStream));
    }

    [Fact]
    public void WriteRecord_WithNullWriter_Throws() {
        var record =
            new RdbRecord(
                "key",
                "value",
                null);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentNullException>(
            () =>
                RdbRecordWriter.WriteRecord(
                    null!,
                    record,
                    now));
    }

    [Fact]
    public void WriteRecord_WithNullRecord_Throws() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentNullException>(
            () =>
                RdbRecordWriter.WriteRecord(
                    writer,
                    null!,
                    now));
    }

    [Fact]
    public void WriteRecord_WithMultipleRecords_CanBeReadSequentially() {
        using var stream = new MemoryStream();

        var writer =
            new RdbBinaryWriter(stream);

        DateTimeOffset now =
            new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

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

        using var readStream =
            new MemoryStream(
                stream.ToArray());

        Assert.Equal(
            (byte)RdbOpcode.StringValue,
            readStream.ReadByte());

        Assert.Equal(
            "first",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            "one",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            (byte)RdbOpcode.ExpireSeconds,
            readStream.ReadByte());

        var binaryReader =
            new BinaryReader(
                readStream);

        Assert.Equal(
            10,
            binaryReader.ReadInt64());

        Assert.Equal(
            (byte)RdbOpcode.StringValue,
            binaryReader.ReadByte());

        Assert.Equal(
            "second",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            "two",
            RdbStringDecoder.ReadString(readStream));

        Assert.Equal(
            readStream.Length,
            readStream.Position);
    }
}