using CacheVault.Core.Models;

namespace CacheVault.Server.Configuration;

public static class EvictionPolicyParser {
    public static EvictionPolicy Parse(
        string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.Trim().ToLowerInvariant() switch {
            "noeviction" =>
                EvictionPolicy.NoEviction,
            "lru" =>
                EvictionPolicy.Lru,
            "allkeys-lru" =>
                EvictionPolicy.Lru,
            "lfu" =>
                EvictionPolicy.Lfu,
            "allkeys-lfu" =>
                EvictionPolicy.Lfu,
            "random" =>
                EvictionPolicy.Random,
            "allkeys-random" =>
                EvictionPolicy.Random,
            _ => throw new ArgumentException(
                $"Unknown eviction policy '{value}'. Supported policies are " +
                "noeviction, lru, lfu, random.",
                nameof(value))
        };
    }
}
