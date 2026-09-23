using System.Text;

namespace CacheVault.Persistence.Rdb.Format;

public static class RdbStringDecoder {
    public static string ReadString(
        Stream stream) {
        ArgumentNullException.ThrowIfNull(stream);

        uint length =
            RdbLengthDecoder.ReadLength(
                stream);

        if (length > int.MaxValue) {
            throw new FormatException(
                "RDB string is too large.");
        }

        byte[] buffer =
            new byte[(int)length];

        ReadExactly(
            stream,
            buffer);

        return Encoding.UTF8.GetString(
            buffer);
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