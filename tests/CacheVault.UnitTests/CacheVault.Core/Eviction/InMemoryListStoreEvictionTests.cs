using System.Text;
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

    [Fact]
    public void MemoryUsageBytes_TracksListMutationsIncrementally()
    {
        var manager =
            new EvictionManager(
                0,
                EvictionPolicy.NoEviction);

        var coordinator =
            new EvictionCoordinator(
                manager);

        var store =
            new InMemoryListStore(
                manager,
                coordinator);

        const string key = "numbers";

        store.PushRight(key, ["one", "é"]);
        Assert.Equal(
            CalculateExpectedMemoryUsage(key, "one", "é"),
            manager.MemoryUsageBytes);

        store.PushLeft(key, ["🙂", "two"]);
        Assert.Equal(
            CalculateExpectedMemoryUsage(key, "two", "🙂", "one", "é"),
            manager.MemoryUsageBytes);

        Assert.Equal("é", store.PopRight(key));
        Assert.Equal(
            CalculateExpectedMemoryUsage(key, "two", "🙂", "one"),
            manager.MemoryUsageBytes);

        Assert.Equal("two", store.PopLeft(key));
        Assert.Equal(
            CalculateExpectedMemoryUsage(key, "🙂", "one"),
            manager.MemoryUsageBytes);

        Assert.True(store.Remove(key));
        Assert.Equal(0, manager.MemoryUsageBytes);
    }

    [Fact]
    public void EvictedList_ClearsIncrementalMemoryStateBeforeKeyReuse()
    {
        // "numbers" has seven UTF-8 bytes.
        // 64 overhead + 7 key bytes + 1 value byte = 72 bytes.
        // A two-byte value exceeds the limit; a one-byte value fits.
        var manager =
            new EvictionManager(
                72,
                EvictionPolicy.Lru);

        var coordinator =
            new EvictionCoordinator(
                manager);

        var store =
            new InMemoryListStore(
                manager,
                coordinator);

        store.PushRight("numbers", ["xx"]);

        Assert.False(store.Contains("numbers"));
        Assert.Equal(0, manager.MemoryUsageBytes);

        // Reusing the key should start with fresh accounting.
        store.PushRight("numbers", ["y"]);

        Assert.True(store.Contains("numbers"));
        Assert.Equal(72, manager.MemoryUsageBytes);
    }

    private static long CalculateExpectedMemoryUsage(
        string key,
        params string[] values)
    {
        long bytes = 64L + Encoding.UTF8.GetByteCount(key);

        foreach (string value in values)
        {
            bytes += Encoding.UTF8.GetByteCount(value);
        }

        return bytes;
    }
}
