using CacheVault.Core.Models;

namespace CacheVault.Core.Lists;

public interface IListStoreSnapshot {
    IReadOnlyList<PersistentList> GetSnapshot();
}
