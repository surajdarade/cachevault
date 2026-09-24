using CacheVault.Core.Eviction;
using CacheVault.Core.Lists;
using CacheVault.Core.Models;

namespace CacheVault.UnitTests.CacheVault.Core.Eviction;

public sealed class InMemoryListStoreEvictionTests
{
    [Fact]
    public void LargeList_ContributesToMemoryUsage()
    {
        var manager =
            new EvictionManager(
                4096,
                EvictionPolicy.Lru);

        var coordinator =
            new EvictionCoordinator(
                manager);

        var store =
            new InMemoryListStore(
                manager,
                coordinator);

        store.PushRight(
            "numbers",
            ["one", "two", "three"]);

        Assert.True(
            manager.MemoryUsageBytes > 0);

        Assert.Contains(
            "numbers",
            store.GetSnapshot().Select(
                list => list.Key));
    }
}
