namespace CacheVault.Core.Models;

public enum EvictionPolicy {
    NoEviction,
    Lru,
    Lfu,
    Random
}
