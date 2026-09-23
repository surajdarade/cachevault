using System.Text;

namespace CacheVault.Persistence.Rdb.Format;

public static class RdbHeaderReader {
    private const int MagicLength = 4;

    public static RdbHeader ReadHeader(
        Stream stream) {
        ArgumentNullException.ThrowIfNull(
            stream);

        Span<byte> magicBuffer =
            stackalloc byte[MagicLength];

        ReadExactly(
            stream,
            magicBuffer);

        string magic =
            Encoding.ASCII.GetString(
                magicBuffer);

        if (!string.Equals(
                magic,
                RdbHeader.Magic,
                StringComparison.Ordinal)) {
            throw new FormatException(
                $"Invalid RDB magic '{magic}'.");
        }

        Span<byte> versionBuffer =
            stackalloc byte[sizeof(ushort)];

        ReadExactly(
            stream,
            versionBuffer);

        ushort version =
            (ushort)(
                versionBuffer[0] |
                (versionBuffer[1] << 8));

        Span<byte> offsetBuffer =
            stackalloc byte[sizeof(long)];

        ReadExactly(
            stream,
            offsetBuffer);

        ulong offsetValue =
            (ulong)offsetBuffer[0] |
            ((ulong)offsetBuffer[1] << 8) |
            ((ulong)offsetBuffer[2] << 16) |
            ((ulong)offsetBuffer[3] << 24) |
            ((ulong)offsetBuffer[4] << 32) |
            ((ulong)offsetBuffer[5] << 40) |
            ((ulong)offsetBuffer[6] << 48) |
            ((ulong)offsetBuffer[7] << 56);

        long aofOffset =
            unchecked(
                (long)offsetValue);

        if (aofOffset < 0) {
            throw new FormatException(
                "RDB AOF offset cannot be negative.");
        }

        return new RdbHeader(
            version,
            aofOffset);
    }

    private static void ReadExactly(
        Stream stream,
        Span<byte> buffer) {
        int totalRead = 0;

        while (totalRead < buffer.Length) {
            int bytesRead =
                stream.Read(
                    buffer[totalRead..]);

            if (bytesRead == 0) {
                throw new EndOfStreamException(
                    "Unexpected end of RDB stream.");
            }

            totalRead += bytesRead;
        }
    }
}