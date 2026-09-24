using CacheVault.Core.Eviction;
using CacheVault.Core.Storage;
using CacheVault.Core.Models;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStoreEvictionTests {
    [Fact]
    public void Set_WhenMemoryLimitIsExceeded_EvictsUsingLru() {
        var clock =
            new TestClock(
                DateTimeOffset.UtcNow);

        var evictionManager =
            new EvictionManager(
                160,
                EvictionPolicy.Lru);

        var store =
            new InMemoryKeyValueStore(
                clock,
                evictionManager);

        store.Set(
            "first",
            "value");

        store.Set(
            "second",
            "value");

        store.TryGet(
            "first",
            out _);

        store.Set(
            "third",
            "value");

        Assert.True(
            store.Contains("first"));

        Assert.False(
            store.Contains("second"));

        Assert.True(
            store.Contains("third"));
    }

    [Fact]
    public void Set_WhenExpiredKeyExists_RemovesExpiredKeyBeforeEviction() {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var clock =
            new TestClock(now);

        var evictionManager =
            new EvictionManager(
                160,
                EvictionPolicy.Lru);

        var store =
            new InMemoryKeyValueStore(
                clock,
                evictionManager);

        store.Set(
            "expired",
            "value",
            now.AddSeconds(1));

        clock.Advance(
            TimeSpan.FromSeconds(2));

        store.Set(
            "current",
            "value");

        Assert.False(
            store.Contains("expired"));

        Assert.True(
            store.Contains("current"));
    }

    [Fact]
    public void Eviction_ChangesKeyVersion() {
        var clock =
            new TestClock(
                DateTimeOffset.UtcNow);

        var evictionManager =
            new EvictionManager(
                160,
                EvictionPolicy.Lru);

        var store =
            new InMemoryKeyValueStore(
                clock,
                evictionManager);

        store.Set(
            "first",
            "value");

        long version =
            store.GetVersion("first");

        store.Set(
            "second",
            "value");

        store.Set(
            "third",
            "value");

        Assert.False(
            store.Contains("first"));

        Assert.True(
            store.GetVersion("first") > version);
    }
}
