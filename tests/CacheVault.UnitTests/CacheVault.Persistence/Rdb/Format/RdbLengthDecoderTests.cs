using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbLengthDecoderTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(63)]
    public void ReadLength_SixBitEncoding_ReturnsExpectedValue(
        uint expected) {
        using var stream =
            new MemoryStream(
                [(byte)expected]);

        uint result =
            RdbLengthDecoder.ReadLength(
                stream);

        Assert.Equal(
            expected,
            result);

        Assert.Equal(
            stream.Length,
            stream.Position);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(8192)]
    [InlineData(16383)]
    public void ReadLength_FourteenBitEncoding_ReturnsExpectedValue(
        uint expected) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            expected);

        stream.Position = 0;

        uint result =
            RdbLengthDecoder.ReadLength(
                stream);

        Assert.Equal(
            expected,
            result);

        Assert.Equal(
            stream.Length,
            stream.Position);
    }

    [Theory]
    [InlineData(16384)]
    [InlineData(65535)]
    [InlineData(1000000)]
    [InlineData(268435456)]
    [InlineData((1u << 30) - 1)]
    public void ReadLength_ThirtyBitEncoding_ReturnsExpectedValue(
        uint expected) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            expected);

        stream.Position = 0;

        uint result =
            RdbLengthDecoder.ReadLength(
                stream);

        Assert.Equal(
            expected,
            result);

        Assert.Equal(
            stream.Length,
            stream.Position);
    }

    [Fact]
    public void ReadLength_EmptyStream_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream();

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbLengthDecoder.ReadLength(
                    stream));
    }

    [Fact]
    public void ReadLength_TruncatedFourteenBitEncoding_ThrowsEndOfStreamException() {
        using var stream =
            new MemoryStream(
                [0x40]);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbLengthDecoder.ReadLength(
                    stream));
    }

    [Theory]
    [InlineData(new byte[] { 0x80 })]
    [InlineData(new byte[] { 0x80, 0x00 })]
    [InlineData(new byte[] { 0x80, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x80, 0x00, 0x00, 0x00 })]
    public void ReadLength_TruncatedThirtyBitEncoding_ThrowsEndOfStreamException(
        byte[] data) {
        using var stream =
            new MemoryStream(data);

        Assert.Throws<EndOfStreamException>(
            () =>
                RdbLengthDecoder.ReadLength(
                    stream));
    }
}