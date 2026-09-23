namespace CacheVault.Core.Models;

public sealed class StoredValue {
    public StoredValue(
        string value,
        DateTimeOffset? expiresAt = null) {
        ArgumentNullException.ThrowIfNull(
            value);

        Value =
            value;

        ExpiresAt =
            expiresAt;
    }

    public string Value { get; }

    public long Version { get; internal set; }

    public DateTimeOffset? ExpiresAt { get; internal set; }
}