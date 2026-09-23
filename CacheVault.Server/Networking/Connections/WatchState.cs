namespace CacheVault.Server.Networking.Connections;

public sealed class WatchState {
    private readonly Dictionary<string, long> _watchedKeys =
        new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, long> WatchedKeys =>
        _watchedKeys;

    public void Watch(
        string key,
        long version) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        _watchedKeys[key] =
            version;
    }

    public bool HasWatchedKeys =>
        _watchedKeys.Count > 0;

    public bool HasChanged(
        Func<string, long> versionProvider) {
        ArgumentNullException.ThrowIfNull(
            versionProvider);

        foreach (KeyValuePair<string, long> watchedKey
            in _watchedKeys) {
            long currentVersion =
                versionProvider(
                    watchedKey.Key);

            if (currentVersion != watchedKey.Value) {
                return true;
            }
        }

        return false;
    }

    public void Clear() {
        _watchedKeys.Clear();
    }
}