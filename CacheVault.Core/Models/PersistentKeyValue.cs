namespace CacheVault.Core.Models;

public sealed class PersistentKeyValue {
    public PersistentKeyValue(
        string key,
        string value,
        DateTimeOffset? expiresAt = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        ArgumentNullException.ThrowIfNull(
            value);

        Key = key;
        Value = value;
        ExpiresAt = expiresAt;
    }

    public string Key { get; }

    public string Value { get; }

    public DateTimeOffset? ExpiresAt { get; }
}