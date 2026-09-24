namespace CacheVault.Core.Eviction.Abstractions;

public interface IEvictionCoordinator {
    void RegisterStringKeyspace(Func<string, bool> remove);

    void RegisterListKeyspace(Func<string, bool> remove);

    void EnforceMemoryLimit();
}
