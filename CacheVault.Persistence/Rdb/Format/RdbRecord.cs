namespace CacheVault.Persistence.Rdb.Format;

public sealed record RdbRecord(
    string Key,
    string Value,
    DateTimeOffset? ExpiresAt);