using System.Text;

namespace CacheVault.Persistence.Rdb.Format;

public static class RdbHeaderReader {
    private const int MagicLength = 4;

    public static RdbHeader ReadHeader(
        Stream stream) {
        ArgumentNullException.ThrowIfNull(stream);

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

        return new RdbHeader(
            version);
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