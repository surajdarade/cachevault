using CacheVault.Core.Models;

namespace CacheVault.Core.Eviction.Abstractions;

public interface IEvictionManager {
    long MemoryUsageBytes { get; }

    long MaxMemoryBytes { get; }

    EvictionPolicy Policy { get; }

    void OnSet(
        string key,
        StoredValue value);

    void OnSet(
        string key,
        long memoryUsageBytes,
        bool isList);

    void OnAccess(
        string key);

    void OnRemove(
        string key);

    bool TrySelectCandidate(
        out string? key);

    bool TrySelectCandidate(
        out string? key,
        out bool isList);
}
