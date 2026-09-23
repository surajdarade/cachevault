using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbHeaderTests {
    [Fact]
    public void WriteHeader_WritesMagicVersionAndAofOffset() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        var header =
            new RdbHeader(
                RdbHeader.CurrentVersion,
                0);

        RdbHeaderWriter.WriteHeader(
            writer,
            header);

        Assert.Equal(
            [
                (byte)'C',
                (byte)'V',
                (byte)'D',
                (byte)'B',
                0x03,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            ],
            stream.ToArray());
    }

    [Fact]
    public void WriteHeader_ThenReadHeader_RoundTrips() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        var expected =
            new RdbHeader(
                42,
                12345);

        RdbHeaderWriter.WriteHeader(
            writer,
            expected);

        stream.Position = 0;

        RdbHeader actual =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void ReadHeader_WithInvalidMagic_ThrowsFormatException() {
        using var stream =
            new MemoryStream(
            [
                (byte)'R',
                (byte)'E',
                (byte)'D',
                (byte)'S',
                0x03,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            ]);

        Assert.Throws<FormatException>(
            () =>
                RdbHeaderReader.ReadHeader(
                    stream));
    }

    [Fact]
    public void ReadHeader_WithTruncatedMagic_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream(
            [
                (byte)'C',
                (byte)'V'
            ]);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbHeaderReader.ReadHeader(
                    stream));
    }

    [Fact]
    public void ReadHeader_WithTruncatedVersion_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream(
            [
                (byte)'C',
                (byte)'V',
                (byte)'D',
                (byte)'B',
                0x03
            ]);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbHeaderReader.ReadHeader(
                    stream));
    }

    [Fact]
    public void ReadHeader_WithTruncatedAofOffset_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream(
            [
                (byte)'C',
                (byte)'V',
                (byte)'D',
                (byte)'B',
                0x03,
                0x00,
                0x39,
                0x30
            ]);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbHeaderReader.ReadHeader(
                    stream));
    }

    [Fact]
    public void ReadHeader_WithNegativeAofOffset_ThrowsFormatException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        var header =
            new RdbHeader(
                RdbHeader.CurrentVersion,
                -1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                RdbHeaderWriter.WriteHeader(
                    writer,
                    header));
    }

    [Fact]
    public void ReadHeader_PreservesCustomVersion() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        var expected =
            new RdbHeader(
                1234,
                987654);

        RdbHeaderWriter.WriteHeader(
            writer,
            expected);

        stream.Position = 0;

        RdbHeader actual =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            1234,
            actual.Version);

        Assert.Equal(
            987654,
            actual.AofOffset);
    }

    [Fact]
    public void ReadHeader_PreservesAofOffset() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        var expected =
            new RdbHeader(
                RdbHeader.CurrentVersion,
                123456789);

        RdbHeaderWriter.WriteHeader(
            writer,
            expected);

        stream.Position = 0;

        RdbHeader actual =
            RdbHeaderReader.ReadHeader(
                stream);

        Assert.Equal(
            123456789,
            actual.AofOffset);
    }
}