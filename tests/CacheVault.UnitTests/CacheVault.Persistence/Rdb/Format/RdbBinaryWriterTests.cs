using System.Buffers.Binary;
using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Format;

public sealed class RdbBinaryWriterTests {
    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () =>
                new RdbBinaryWriter(null!));
    }

    [Fact]
    public void Constructor_WithReadOnlyStream_ThrowsArgumentException() {
        using var stream =
            new MemoryStream(
                new byte[10],
                writable: false);

        Assert.Throws<ArgumentException>(
            () =>
                new RdbBinaryWriter(stream));
    }

    [Fact]
    public void WriteByte_WritesSingleByte() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteByte(
            0xAB);

        Assert.Equal(
            [0xAB],
            stream.ToArray());
    }

    [Fact]
    public void WriteUInt16_WritesLittleEndian() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteUInt16(
            0x1234);

        Assert.Equal(
            [0x34, 0x12],
            stream.ToArray());
    }

    [Fact]
    public void WriteUInt32_WritesLittleEndian() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteUInt32(
            0x12345678);

        Assert.Equal(
            [0x78, 0x56, 0x34, 0x12],
            stream.ToArray());
    }

    [Fact]
    public void WriteUInt64_WritesLittleEndian() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteUInt64(
            0x0123456789ABCDEF);

        Assert.Equal(
            [
                0xEF,
                0xCD,
                0xAB,
                0x89,
                0x67,
                0x45,
                0x23,
                0x01
            ],
            stream.ToArray());
    }

    [Fact]
    public void WriteInt64_WritesSignedValueAsLittleEndian() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        long value =
            -1234567890123456789L;

        writer.WriteInt64(
            value);

        byte[] expected =
            new byte[sizeof(long)];

        BinaryPrimitives.WriteInt64LittleEndian(
            expected,
            value);

        Assert.Equal(
            expected,
            stream.ToArray());
    }

    [Fact]
    public void WriteBytes_WritesAllBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        byte[] bytes =
        [
            0x01,
            0x02,
            0x03,
            0xFF
        ];

        writer.WriteBytes(
            bytes);

        Assert.Equal(
            bytes,
            stream.ToArray());
    }

    [Fact]
    public void MultipleWrites_PreserveOrder() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteByte(
            0xAA);

        writer.WriteUInt16(
            0x1234);

        writer.WriteUInt32(
            0x56789ABC);

        writer.WriteByte(
            0xFF);

        Assert.Equal(
            [
                0xAA,
                0x34,
                0x12,
                0xBC,
                0x9A,
                0x78,
                0x56,
                0xFF
            ],
            stream.ToArray());
    }

    [Fact]
    public void WriteInt64_WithMinValue_WritesCorrectBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteInt64(
            long.MinValue);

        Assert.Equal(
            [
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x80
            ],
            stream.ToArray());
    }

    [Fact]
    public void WriteInt64_WithMaxValue_WritesCorrectBytes() {
        using var stream =
            new MemoryStream();

        var writer =
            new RdbBinaryWriter(
                stream);

        writer.WriteInt64(
            long.MaxValue);

        Assert.Equal(
            [
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0x7F
            ],
            stream.ToArray());
    }
}