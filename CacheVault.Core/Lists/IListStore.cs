namespace CacheVault.Core.Lists;

public interface IListStore {
    int Length(string key);

    int PushLeft(
        string key,
        IReadOnlyList<string> values);

    int PushRight(
        string key,
        IReadOnlyList<string> values);

    string? PopLeft(string key);

    string? PopRight(string key);

    IReadOnlyList<string> Range(
        string key,
        long start,
        long stop);

    bool Remove(string key);

    bool Contains(string key);

    long GetVersion(string key);
}