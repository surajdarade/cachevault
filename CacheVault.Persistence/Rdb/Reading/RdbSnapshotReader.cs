using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.Persistence.Rdb.Reading;

public sealed class RdbSnapshotReader {
    public IReadOnlyList<RdbRecord> Read(
        Stream stream,
        DateTimeOffset now) {
        RdbSnapshotReadResult result =
            ReadWithMetadata(
                stream,
                now);

        return result.Records;
    }

    public RdbSnapshotReadResult ReadWithMetadata(
        Stream stream,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (!stream.CanRead) {
            throw new ArgumentException(
                "The stream must be readable.",
                nameof(stream));
        }

        RdbHeader header =
            RdbHeaderReader.ReadHeader(
                stream);

        ValidateVersion(
            header);

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
                return new RdbSnapshotReadResult(
                    header,
                    records);
            }

            RdbRecord record =
                ReadRecordWithOpcode(
                    stream,
                    opcode);

            records.Add(
                record);
        }
    }

    private static RdbRecord ReadRecordWithOpcode(
        Stream stream,
        RdbOpcode opcode) {
        if (opcode == RdbOpcode.ExpireMilliseconds ||
            opcode == RdbOpcode.ExpireSeconds) {
            return ReadRecordWithExpiration(
                stream,
                opcode);
        }

        if (opcode == RdbOpcode.StringValue) {
            return new RdbRecord(
                RdbStringDecoder.ReadString(stream),
                RdbStringDecoder.ReadString(stream),
                null);
        }

        if (opcode == RdbOpcode.ListValue) {
            return ReadListRecord(stream);
        }

        throw new FormatException(
            $"Unsupported RDB record opcode: " +
            $"0x{(byte)opcode:X2}.");
    }

    private static RdbRecord ReadRecordWithExpiration(
        Stream stream,
        RdbOpcode opcode) {
        long timestamp =
            ReadInt64(stream);

        DateTimeOffset expiresAt;

        try {
            expiresAt =
                opcode == RdbOpcode.ExpireMilliseconds
                    ? DateTimeOffset.FromUnixTimeMilliseconds(
                        timestamp)
                    : DateTimeOffset.FromUnixTimeSeconds(
                        timestamp);
        }
        catch (ArgumentOutOfRangeException) {
            throw new FormatException(
                "RDB expiration timestamp is outside the supported range.");
        }

        RdbOpcode valueOpcode =
            ReadNextRecordOpcode(stream);

        /*
         * Expiration metadata currently applies only to
         * string records.
         *
         * A list opcode or any other opcode cannot legally
         * follow expiration metadata. Report it as an
         * unsupported RDB record opcode rather than treating
         * it as a generic expiration/type error.
         */
        if (valueOpcode != RdbOpcode.StringValue) {
            throw new FormatException(
                $"Unsupported RDB record opcode: " +
                $"0x{(byte)valueOpcode:X2}.");
        }

        return new RdbRecord(
            RdbStringDecoder.ReadString(stream),
            RdbStringDecoder.ReadString(stream),
            expiresAt);
    }

    private static RdbRecord ReadListRecord(
        Stream stream) {
        string key =
            RdbStringDecoder.ReadString(stream);

        uint count =
            RdbLengthDecoder.ReadLength(stream);

        if (count > int.MaxValue) {
            throw new FormatException(
                "RDB list contains too many elements.");
        }

        var values =
            new string[(int)count];

        for (int index = 0;
             index < values.Length;
             index++) {
            values[index] =
                RdbStringDecoder.ReadString(stream);
        }

        return RdbRecord.CreateList(
            key,
            values);
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