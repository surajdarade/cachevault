using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.Persistence.Rdb.Reading;

public sealed class RdbSnapshotReader {
    public IReadOnlyList<RdbRecord> Read(
        Stream stream,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead) {
            throw new ArgumentException(
                "The stream must be readable.",
                nameof(stream));
        }

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                stream);

        ValidateVersion(header);

        var records =
            new List<RdbRecord>();

        while (true) {
            int opcodeValue =
                stream.ReadByte();

            if (opcodeValue < 0) {
                throw new EndOfStreamException(
                    "RDB snapshot is missing the end-of-file marker.");
            }

            RdbOpcode opcode =
                (RdbOpcode)opcodeValue;

            if (opcode ==
                RdbOpcode.EndOfFile) {
                return records;
            }

            RdbRecord record =
                ReadRecordWithOpcode(
                    stream,
                    opcode,
                    now);

            records.Add(record);
        }
    }

    private static RdbRecord ReadRecordWithOpcode(
        Stream stream,
        RdbOpcode opcode,
        DateTimeOffset now) {
        DateTimeOffset? expiresAt = null;

        if (opcode ==
            RdbOpcode.ExpireMilliseconds) {
            long milliseconds =
                ReadInt64(stream);

            if (milliseconds <= 0) {
                throw new FormatException(
                    "RDB expiration must be greater than zero.");
            }

            expiresAt =
                now.AddMilliseconds(
                    milliseconds);

            opcode =
                ReadNextRecordOpcode(
                    stream);
        }
        else if (opcode ==
                 RdbOpcode.ExpireSeconds) {
            long seconds =
                ReadInt64(stream);

            if (seconds <= 0) {
                throw new FormatException(
                    "RDB expiration must be greater than zero.");
            }

            expiresAt =
                now.AddSeconds(
                    seconds);

            opcode =
                ReadNextRecordOpcode(
                    stream);
        }

        if (opcode !=
            RdbOpcode.StringValue) {
            throw new FormatException(
                $"Unsupported RDB record opcode: " +
                $"0x{(byte)opcode:X2}.");
        }

        return ReadStringRecord(
            stream,
            expiresAt);
    }

    private static RdbOpcode ReadNextRecordOpcode(
        Stream stream) {
        int opcodeValue =
            stream.ReadByte();

        if (opcodeValue < 0) {
            throw new EndOfStreamException(
                "Unexpected end of RDB stream.");
        }

        return (RdbOpcode)opcodeValue;
    }

    private static RdbRecord ReadStringRecord(
        Stream stream,
        DateTimeOffset? expiresAt) {
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

    private static void ValidateVersion(
        RdbHeader header) {
        if (header.Version !=
            RdbHeader.CurrentVersion) {
            throw new FormatException(
                $"Unsupported RDB version: " +
                $"{header.Version}.");
        }
    }
}