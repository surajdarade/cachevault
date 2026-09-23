using CacheVault.Core.Models;
using CacheVault.Core.Storage;
using CacheVault.Infrastructure.Time;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStoreSnapshotTests {
    private readonly TestClock _clock;
    private readonly InMemoryKeyValueStore _store;

    public InMemoryKeyValueStoreSnapshotTests() {
        _clock =
            new TestClock(
                DateTimeOffset.UtcNow);

        _store =
            new InMemoryKeyValueStore(
                _clock);
    }

    [Fact]
    public void GetSnapshot_EmptyStore_ReturnsEmptySnapshot() {
        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        Assert.Empty(snapshot);
    }

    [Fact]
    public void GetSnapshot_WithSingleKey_ReturnsKey() {
        _store.Set(
            "key",
            "value");

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        PersistentKeyValue item =
            Assert.Single(snapshot);

        Assert.Equal(
            "key",
            item.Key);

        Assert.Equal(
            "value",
            item.Value);
    }

    [Fact]
    public void GetSnapshot_WithMultipleKeys_ReturnsAllKeys() {
        _store.Set(
            "key1",
            "value1");

        _store.Set(
            "key2",
            "value2");

        _store.Set(
            "key3",
            "value3");

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        Assert.Equal(
            3,
            snapshot.Count);

        Assert.Contains(
            snapshot,
            item =>
                item.Key == "key1" &&
                item.Value == "value1");

        Assert.Contains(
            snapshot,
            item =>
                item.Key == "key2" &&
                item.Value == "value2");

        Assert.Contains(
            snapshot,
            item =>
                item.Key == "key3" &&
                item.Value == "value3");
    }

    [Fact]
    public void GetSnapshot_PreservesExpiration() {
        DateTimeOffset expiresAt =
            _clock.UtcNow.AddMinutes(5);

        _store.Set(
            "key",
            "value",
            expiresAt);

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        PersistentKeyValue item =
            Assert.Single(snapshot);

        Assert.Equal(
            expiresAt,
            item.ExpiresAt);
    }

    [Fact]
    public void GetSnapshot_WithNoExpiration_ReturnsNullExpiration() {
        _store.Set(
            "key",
            "value");

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        PersistentKeyValue item =
            Assert.Single(snapshot);

        Assert.Null(
            item.ExpiresAt);
    }

    [Fact]
    public void GetSnapshot_DoesNotIncludeExpiredKeys() {
        DateTimeOffset expiresAt =
            _clock.UtcNow.AddMinutes(1);

        _store.Set(
            "expired",
            "value",
            expiresAt);

        _clock.Advance(
            TimeSpan.FromMinutes(1));

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        Assert.Empty(snapshot);
    }

    [Fact]
    public void GetSnapshot_IncludesUnexpiredKeys_WhenOtherKeyExpired() {
        DateTimeOffset expiresAt =
            _clock.UtcNow.AddMinutes(1);

        _store.Set(
            "expired",
            "expired-value",
            expiresAt);

        _store.Set(
            "persistent",
            "persistent-value");

        _clock.Advance(
            TimeSpan.FromMinutes(1));

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        PersistentKeyValue item =
            Assert.Single(snapshot);

        Assert.Equal(
            "persistent",
            item.Key);

        Assert.Equal(
            "persistent-value",
            item.Value);
    }

    [Fact]
    public void GetSnapshot_IsIndependentOfSubsequentStoreMutation() {
        _store.Set(
            "key",
            "original");

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        _store.Set(
            "key",
            "updated");

        PersistentKeyValue item =
            Assert.Single(snapshot);

        Assert.Equal(
            "original",
            item.Value);
    }

    [Fact]
    public void GetSnapshot_DoesNotIncludeVersion() {
        _store.Set(
            "key",
            "value");

        IReadOnlyList<PersistentKeyValue> snapshot =
            _store.GetSnapshot();

        PersistentKeyValue item =
            Assert.Single(snapshot);

        Assert.Equal(
            "key",
            item.Key);

        Assert.Equal(
            "value",
            item.Value);

        Assert.Null(
            item.ExpiresAt);
    }
}