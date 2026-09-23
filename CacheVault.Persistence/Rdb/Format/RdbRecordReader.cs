namespace CacheVault.Persistence.Rdb.Format;

public static class RdbRecordReader {
    public static RdbRecord ReadRecord(
        Stream stream,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(stream);

        int firstByte =
            stream.ReadByte();

        if (firstByte < 0) {
            throw new EndOfStreamException(
                "Unexpected end of RDB stream.");
        }

        RdbOpcode firstOpcode =
            (RdbOpcode)firstByte;

        DateTimeOffset? expiresAt = null;

        if (firstOpcode ==
            RdbOpcode.ExpireMilliseconds) {
            long milliseconds =
                ReadInt64(stream);

            try {
                expiresAt =
                    DateTimeOffset.FromUnixTimeMilliseconds(
                        milliseconds);
            }
            catch (ArgumentOutOfRangeException exception) {
                throw new FormatException(
                    "RDB expiration timestamp is invalid.",
                    exception);
            }

            firstOpcode =
                ReadNextOpcode(stream);
        }
        else if (firstOpcode ==
                 RdbOpcode.ExpireSeconds) {
            long seconds =
                ReadInt64(stream);

            try {
                expiresAt =
                    DateTimeOffset.FromUnixTimeSeconds(
                        seconds);
            }
            catch (ArgumentOutOfRangeException exception) {
                throw new FormatException(
                    "RDB expiration timestamp is invalid.",
                    exception);
            }

            firstOpcode =
                ReadNextOpcode(stream);
        }

        if (firstOpcode !=
            RdbOpcode.StringValue) {
            throw new FormatException(
                $"Unsupported RDB record opcode: " +
                $"0x{(byte)firstOpcode:X2}.");
        }

        string key =
            RdbStringDecoder.ReadString(
                stream);

        string value =
            RdbStringDecoder.ReadString(
                stream);

        return new RdbRecord(
            key,
            value,
            expiresAt);
    }

    private static RdbOpcode ReadNextOpcode(
        Stream stream) {
        int value =
            stream.ReadByte();

        if (value < 0) {
            throw new EndOfStreamException(
                "Unexpected end of RDB stream.");
        }

        return (RdbOpcode)value;
    }

    private static long ReadInt64(
        Stream stream) {
        Span<byte> buffer =
            stackalloc byte[sizeof(long)];

        ReadExactly(
            stream,
            buffer);

        ulong value =
            (ulong)buffer[0] |
            ((ulong)buffer[1] << 8) |
            ((ulong)buffer[2] << 16) |
            ((ulong)buffer[3] << 24) |
            ((ulong)buffer[4] << 32) |
            ((ulong)buffer[5] << 40) |
            ((ulong)buffer[6] << 48) |
            ((ulong)buffer[7] << 56);

        return unchecked(
            (long)value);
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