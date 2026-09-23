using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbLengthEncoderTests {
    [Theory]
    [InlineData(0, 0x00)]
    [InlineData(1, 0x01)]
    [InlineData(63, 0x3F)]
    public void WriteLength_SixBitValues_WritesSingleByte(
        uint length,
        byte expected) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            length);

        Assert.Equal(
            [expected],
            stream.ToArray());
    }

    [Fact]
    public void WriteLength_FourteenBitMinimum_WritesTwoBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            64);

        Assert.Equal(
            [0x40, 0x40],
            stream.ToArray());
    }

    [Fact]
    public void WriteLength_FourteenBitMaximum_WritesTwoBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            16383);

        Assert.Equal(
            [0x7F, 0xFF],
            stream.ToArray());
    }

    [Theory]
    [InlineData(64, 0x40, 0x40)]
    [InlineData(255, 0x40, 0xFF)]
    [InlineData(256, 0x41, 0x00)]
    [InlineData(1024, 0x44, 0x00)]
    [InlineData(4096, 0x50, 0x00)]
    [InlineData(8192, 0x60, 0x00)]
    [InlineData(16383, 0x7F, 0xFF)]
    public void WriteLength_FourteenBitValues_WritesExpectedBytes(
        uint length,
        byte expectedFirstByte,
        byte expectedSecondByte) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            length);

        Assert.Equal(
            [
                expectedFirstByte,
                expectedSecondByte
            ],
            stream.ToArray());
    }

    [Fact]
    public void WriteLength_ThirtyBitMinimum_WritesFiveBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            16384);

        Assert.Equal(
            [
                0x80,
                0x00,
                0x00,
                0x40,
                0x00
            ],
            stream.ToArray());
    }

    [Fact]
    public void WriteLength_ThirtyBitMaximum_WritesFiveBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            (1u << 30) - 1);

        Assert.Equal(
            [
                0x80,
                0x3F,
                0xFF,
                0xFF,
                0xFF
            ],
            stream.ToArray());
    }

    [Theory]
    [InlineData(16384)]
    [InlineData(65535)]
    [InlineData(1000000)]
    [InlineData(268435456)]
    [InlineData((1u << 30) - 1)]
    public void WriteLength_ThirtyBitValues_WritesFiveBytes(
        uint length) {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        RdbLengthEncoder.WriteLength(
            writer,
            length);

        byte[] bytes =
            stream.ToArray();

        Assert.Equal(
            5,
            bytes.Length);

        Assert.Equal(
            0x80,
            bytes[0]);

        uint encodedValue =
            ((uint)bytes[1] << 24) |
            ((uint)bytes[2] << 16) |
            ((uint)bytes[3] << 8) |
            bytes[4];

        Assert.Equal(
            length,
            encodedValue);
    }

    [Fact]
    public void WriteLength_ExceedingThirtyBits_ThrowsArgumentOutOfRangeException() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        uint invalidLength =
            1u << 30;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                RdbLengthEncoder.WriteLength(
                    writer,
                    invalidLength));
    }
}