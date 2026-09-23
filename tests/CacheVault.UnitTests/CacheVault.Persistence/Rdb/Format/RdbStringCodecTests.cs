using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbStringCodecTests {
    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("CacheVault")]
    [InlineData("Redis-compatible database")]
    public void WriteString_ThenReadString_RoundTrips(
        string value) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbStringEncoder.WriteString(
            writer,
            value);

        stream.Position = 0;

        string result =
            RdbStringDecoder.ReadString(
                stream);

        Assert.Equal(
            value,
            result);
    }

    [Fact]
    public void WriteString_UsesUtf8() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbStringEncoder.WriteString(
            writer,
            "héllo 世界");

        stream.Position = 0;

        string result =
            RdbStringDecoder.ReadString(
                stream);

        Assert.Equal(
            "héllo 世界",
            result);
    }

    [Fact]
    public void WriteString_WithSixtyThreeByteValue_UsesOneByteLength() {
        string value =
            new(
                'a',
                63);

        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbStringEncoder.WriteString(
            writer,
            value);

        byte[] data =
            stream.ToArray();

        Assert.Equal(
            64,
            data.Length);

        Assert.Equal(
            0x3F,
            data[0]);
    }

    [Fact]
    public void WriteString_WithSixtyFourByteValue_UsesTwoByteLength() {
        string value =
            new(
                'a',
                64);

        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbStringEncoder.WriteString(
            writer,
            value);

        byte[] data =
            stream.ToArray();

        Assert.Equal(
            66,
            data.Length);

        Assert.Equal(
            0x40,
            data[0]);

        Assert.Equal(
            0x40,
            data[1]);
    }

    [Fact]
    public void ReadString_TruncatedData_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream(
            [
                0x05,
                (byte)'h',
                (byte)'e'
            ]);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbStringDecoder.ReadString(
                    stream));
    }

    [Fact]
    public void ReadString_WithLargeLengthEncoding_RoundTrips() {
        string value =
            new(
                'x',
                16384);

        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbStringEncoder.WriteString(
            writer,
            value);

        stream.Position = 0;

        string result =
            RdbStringDecoder.ReadString(
                stream);

        Assert.Equal(
            value,
            result);
    }
}