using CacheVault.Core.Models;

namespace CacheVault.Core.Abstractions;

public interface IKeyValueStoreSnapshot {
    IReadOnlyList<PersistentKeyValue> GetSnapshot();
}