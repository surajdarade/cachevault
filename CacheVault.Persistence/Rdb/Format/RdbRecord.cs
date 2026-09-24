namespace CacheVault.Persistence.Rdb.Format;

public sealed record RdbRecord(
    string Key,
    string Value,
    DateTimeOffset? ExpiresAt,
    RdbRecordType Type = RdbRecordType.String,
    IReadOnlyList<string>? ListValues = null)
{
    public static RdbRecord CreateList(
        string key,
        IReadOnlyList<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(values);

        return new RdbRecord(
            key,
            string.Empty,
            null,
            RdbRecordType.List,
            values.ToArray());
    }
}
