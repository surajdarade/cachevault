namespace CacheVault.Persistence.Rdb.Format;

public static class RdbRecordWriter {
    public static void WriteRecord(
        RdbBinaryWriter writer,
        RdbRecord record,
        DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(record);

        if (record.ExpiresAt.HasValue) {
            WriteExpiration(
                writer,
                record.ExpiresAt.Value,
                now);
        }

        writer.WriteByte(
            (byte)RdbOpcode.StringValue);

        RdbStringEncoder.WriteString(
            writer,
            record.Key);

        RdbStringEncoder.WriteString(
            writer,
            record.Value);
    }

    private static void WriteExpiration(
        RdbBinaryWriter writer,
        DateTimeOffset expiresAt,
        DateTimeOffset now) {
        TimeSpan remaining =
            expiresAt - now;

        if (remaining <= TimeSpan.Zero) {
            throw new ArgumentException(
                "An expired record cannot be persisted.",
                nameof(expiresAt));
        }

        long milliseconds =
            checked(
                (long)remaining.TotalMilliseconds);

        if (milliseconds % 1000 == 0) {
            writer.WriteByte(
                (byte)RdbOpcode.ExpireSeconds);

            long seconds =
                milliseconds / 1000;

            writer.WriteInt64(
                seconds);

            return;
        }

        writer.WriteByte(
            (byte)RdbOpcode.ExpireMilliseconds);

        writer.WriteInt64(
            milliseconds);
    }
}