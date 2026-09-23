namespace CacheVault.Persistence.Rdb.Format;

public static class RdbLengthEncoder {
    private const byte SixBitLengthMask = 0x3F;
    private const byte FourteenBitLengthPrefix = 0x40;
    private const byte ThirtyBitLengthPrefix = 0x80;

    private const uint MaximumSixBitLength =
        (1u << 6) - 1;

    private const uint MaximumFourteenBitLength =
        (1u << 14) - 1;

    private const uint MaximumThirtyBitLength =
        (1u << 30) - 1;

    public static void WriteLength(
        RdbBinaryWriter writer,
        uint length) {
        ArgumentNullException.ThrowIfNull(
            writer);

        if (length <= MaximumSixBitLength) {
            writer.WriteByte(
                (byte)length);

            return;
        }

        if (length <= MaximumFourteenBitLength) {
            byte firstByte =
                (byte)(
                    FourteenBitLengthPrefix |
                    (length >> 8));

            byte secondByte =
                (byte)length;

            writer.WriteByte(
                firstByte);

            writer.WriteByte(
                secondByte);

            return;
        }

        if (length <= MaximumThirtyBitLength) {
            byte firstByte =
                ThirtyBitLengthPrefix;

            writer.WriteByte(
                firstByte);

            writer.WriteByte(
                (byte)(length >> 24));

            writer.WriteByte(
                (byte)(length >> 16));

            writer.WriteByte(
                (byte)(length >> 8));

            writer.WriteByte(
                (byte)length);

            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(length),
            length,
            "RDB length cannot exceed 30 bits.");
    }
}