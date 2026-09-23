namespace CacheVault.Persistence.Rdb.Format;

public static class RdbLengthDecoder {
    private const byte EncodingMask = 0xC0;

    private const byte SixBitEncoding = 0x00;
    private const byte FourteenBitEncoding = 0x40;
    private const byte ThirtyBitEncoding = 0x80;

    public static uint ReadLength(
        Stream stream) {
        ArgumentNullException.ThrowIfNull(stream);

        int firstByte =
            stream.ReadByte();

        if (firstByte < 0) {
            throw new EndOfStreamException(
                "Unexpected end of RDB stream.");
        }

        byte first =
            (byte)firstByte;

        byte encoding =
            (byte)(first & EncodingMask);

        return encoding switch
        {
            SixBitEncoding =>
                (uint)(first & 0x3F),

            FourteenBitEncoding =>
                ReadFourteenBitLength(
                    stream,
                    first),

            ThirtyBitEncoding =>
                ReadThirtyBitLength(
                    stream),

            _ =>
                throw new FormatException(
                    "Unsupported RDB length encoding.")
        };
    }

    private static uint ReadFourteenBitLength(
        Stream stream,
        byte firstByte) {
        int secondByte =
            stream.ReadByte();

        if (secondByte < 0) {
            throw new EndOfStreamException(
                "Unexpected end of RDB stream.");
        }

        return
            ((uint)(firstByte & 0x3F) << 8) |
            (byte)secondByte;
    }

    private static uint ReadThirtyBitLength(
        Stream stream) {
        int firstValueByte =
            stream.ReadByte();

        int secondValueByte =
            stream.ReadByte();

        int thirdValueByte =
            stream.ReadByte();

        int fourthValueByte =
            stream.ReadByte();

        if (firstValueByte < 0 ||
            secondValueByte < 0 ||
            thirdValueByte < 0 ||
            fourthValueByte < 0) {
            throw new EndOfStreamException(
                "Unexpected end of RDB stream.");
        }

        return
            ((uint)firstValueByte << 24) |
            ((uint)secondValueByte << 16) |
            ((uint)thirdValueByte << 8) |
            (byte)fourthValueByte;
    }
}