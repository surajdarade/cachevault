namespace CacheVault.Core.Models;

public sealed class StoredValue {
    public StoredValue(string value) {
        Value = value;
    }

    public string Value { get; }

    public long Version { get; internal set; }
}