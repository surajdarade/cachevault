using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbHeaderTests {
    [Fact]
    public void WriteHeader_WritesMagicAndVersion() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        var header =
            new RdbHeader(
                RdbHeader.CurrentVersion);

        RdbHeaderWriter.WriteHeader(
            writer,
            header);

        Assert.Equal(
            [
                (byte)'C',
                (byte)'V',
                (byte)'D',
                (byte)'B',
                0x01,
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
                42);

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
                0x01,
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
                0x01
            ]);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbHeaderReader.ReadHeader(
                    stream));
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
                1234);

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
    }
}