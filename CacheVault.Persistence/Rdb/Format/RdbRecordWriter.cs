namespace CacheVault.Persistence.Rdb.Format;

public static class RdbRecordWriter
{
    public static void WriteRecord(
        RdbBinaryWriter writer,
        RdbRecord record,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Type == RdbRecordType.List)
        {
            WriteListRecord(
                writer,
                record);

            return;
        }

        if (record.ExpiresAt.HasValue)
        {
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

    private static void WriteListRecord(
        RdbBinaryWriter writer,
        RdbRecord record)
    {
        IReadOnlyList<string> values =
            record.ListValues ?? [];

        writer.WriteByte(
            (byte)RdbOpcode.ListValue);

        RdbStringEncoder.WriteString(
            writer,
            record.Key);

        RdbLengthEncoder.WriteLength(
            writer,
            checked((uint)values.Count));

        foreach (string value in values)
        {
            RdbStringEncoder.WriteString(
                writer,
                value);
        }
    }

    private static void WriteExpiration(
        RdbBinaryWriter writer,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (expiresAt <= now)
        {
            throw new ArgumentException(
                "An expired record cannot be persisted.",
                nameof(expiresAt));
        }

        long milliseconds =
            expiresAt.ToUnixTimeMilliseconds();

        if (milliseconds % 1000 == 0)
        {
            writer.WriteByte(
                (byte)RdbOpcode.ExpireSeconds);

            writer.WriteInt64(
                expiresAt.ToUnixTimeSeconds());

            return;
        }

        writer.WriteByte(
            (byte)RdbOpcode.ExpireMilliseconds);

        writer.WriteInt64(
            milliseconds);
    }
}
