using CacheVault.Core.Eviction;
using CacheVault.Core.Models;

namespace CacheVault.UnitTests.CacheVault.Core.Eviction;

public sealed class EvictionManagerTests {
    [Fact]
    public void NoEviction_DoesNotSelectCandidate() {
        var manager =
            new EvictionManager(
                128,
                EvictionPolicy.NoEviction);

        manager.OnSet(
            "key",
            new StoredValue("value"));

        Assert.False(
            manager.TrySelectCandidate(
                out _));

        Assert.True(
            manager.MemoryUsageBytes > 0);
    }

    [Fact]
    public void Lru_SelectsLeastRecentlyAccessedKey() {
        var manager =
            new EvictionManager(
                128,
                EvictionPolicy.Lru);

        manager.OnSet(
            "first",
            new StoredValue("1"));

        manager.OnSet(
            "second",
            new StoredValue("2"));

        manager.OnAccess("first");

        Assert.True(
            manager.TrySelectCandidate(
                out string? candidate));

        Assert.Equal(
            "second",
            candidate);
    }

    [Fact]
    public void Lfu_SelectsLeastFrequentlyUsedKey() {
        var manager =
            new EvictionManager(
                128,
                EvictionPolicy.Lfu);

        manager.OnSet(
            "first",
            new StoredValue("1"));

        manager.OnSet(
            "second",
            new StoredValue("2"));

        manager.OnAccess("first");
        manager.OnAccess("first");

        Assert.True(
            manager.TrySelectCandidate(
                out string? candidate));

        Assert.Equal(
            "second",
            candidate);
    }

    [Fact]
    public void Lfu_WhenFrequencyMatches_UsesLeastRecentlyAccessedKey() {
        var manager =
            new EvictionManager(
                128,
                EvictionPolicy.Lfu);

        manager.OnSet(
            "first",
            new StoredValue("1"));

        manager.OnSet(
            "second",
            new StoredValue("2"));

        Assert.True(
            manager.TrySelectCandidate(
                out string? candidate));

        Assert.Equal(
            "first",
            candidate);
    }

    [Fact]
    public void OnSet_ReplacesExistingMemoryAccounting() {
        var manager =
            new EvictionManager(
                1024,
                EvictionPolicy.Lru);

        manager.OnSet(
            "key",
            new StoredValue("short"));

        long firstUsage =
            manager.MemoryUsageBytes;

        manager.OnSet(
            "key",
            new StoredValue("a much longer value"));

        Assert.True(
            manager.MemoryUsageBytes > firstUsage);
    }

    [Fact]
    public void OnRemove_DecreasesMemoryUsage() {
        var manager =
            new EvictionManager(
                1024,
                EvictionPolicy.Lru);

        manager.OnSet(
            "key",
            new StoredValue("value"));

        long usage =
            manager.MemoryUsageBytes;

        manager.OnRemove("key");

        Assert.Equal(
            0,
            manager.MemoryUsageBytes);

        Assert.True(
            usage > 0);
    }
}
