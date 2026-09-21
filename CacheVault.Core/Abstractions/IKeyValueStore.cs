using CacheVault.Core.Models;

namespace CacheVault.Core.Abstractions;

public interface IKeyValueStore {
    bool TryGet(
        string key,
        out StoredValue? value);

    void Set(
        string key,
        string value);

    bool Remove(
        string key);

    bool Contains(
        string key);

    long Increment(
        string key,
        long amount = 1);
}