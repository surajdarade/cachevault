using System.Text;
using System.Threading.Tasks;
using CacheVault.Core.Eviction;
using CacheVault.Core.Eviction.Abstractions;
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

    [Fact]
    public void PushOperations_InvokeEvictionAfterReleasingTheListKeyLock()
    {
        var coordinator = new LockCheckingEvictionCoordinator();

        var store =
            new InMemoryListStore(
                evictionCoordinator: coordinator);

        coordinator.OnEnforce = () =>
        {
            Task<bool> accessTask =
                Task.Run(() => store.Contains("numbers"));

            Assert.True(
                accessTask.Wait(TimeSpan.FromSeconds(2)),
                "Eviction enforcement ran while the list key lock was held.");

            Assert.True(accessTask.Result);
        };

        store.PushRight("numbers", ["one"]);
        store.PushLeft("numbers", ["two"]);
    }

    [Fact]
    public async Task ConcurrentPushesAcrossKeys_WithEvictionCompleteWithinTimeout()
    {
        const int workerCount = 8;
        const int operationsPerWorker = 300;
        const int keyCount = 16;

        var manager =
            new EvictionManager(
                512,
                EvictionPolicy.Lru);

        var coordinator =
            new EvictionCoordinator(manager);

        var store =
            new InMemoryListStore(
                manager,
                coordinator);

        Task[] workers =
            Enumerable.Range(0, workerCount)
                .Select(worker => Task.Run(() =>
                {
                    for (int operation = 0;
                         operation < operationsPerWorker;
                         operation++)
                    {
                        string key =
                            $"list-{(worker + operation) % keyCount}";

                        store.PushRight(key, ["x"]);
                    }
                }))
                .ToArray();

        await Task.WhenAll(workers)
            .WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(
            manager.MemoryUsageBytes <= manager.MaxMemoryBytes,
            $"Memory usage {manager.MemoryUsageBytes} exceeded limit " +
            $"{manager.MaxMemoryBytes} after concurrent operations.");
    }

    private sealed class LockCheckingEvictionCoordinator
        : IEvictionCoordinator
    {
        public Action? OnEnforce { get; set; }

        public void RegisterStringKeyspace(
            Func<string, bool> remove)
        {
        }

        public void RegisterListKeyspace(
            Func<string, bool> remove)
        {
        }

        public void EnforceMemoryLimit()
        {
            OnEnforce?.Invoke();
        }
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
