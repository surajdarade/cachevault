using CacheVault.Core.Storage;
using CacheVault.UnitTests.CacheVault.Infrastructure;

namespace CacheVault.UnitTests.CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStoreTests {
    private readonly TestClock _clock =
        new(DateTimeOffset.UtcNow);

    private readonly InMemoryKeyValueStore _store;

    public InMemoryKeyValueStoreTests() {
        _store =
            new InMemoryKeyValueStore(
                _clock);
    }


    [Fact]
    public void Set_ThenGet_ReturnsValue() {
        _store.Set("name", "Suraj");

        bool found = _store.TryGet(
            "name",
            out var value);

        Assert.True(found);
        Assert.NotNull(value);
        Assert.Equal("Suraj", value.Value);
    }

    [Fact]
    public void Get_MissingKey_ReturnsFalse() {
        bool found = _store.TryGet(
            "missing",
            out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_ExistingKey_ReplacesValue() {
        _store.Set("name", "Suraj");
        _store.Set("name", "Darade");

        bool found = _store.TryGet(
            "name",
            out var value);

        Assert.True(found);
        Assert.NotNull(value);
        Assert.Equal("Darade", value.Value);
    }

    [Fact]
    public void Set_AssignsVersion() {
        _store.Set("key", "value");

        bool found = _store.TryGet(
            "key",
            out var value);

        Assert.True(found);
        Assert.NotNull(value);
        Assert.True(value.Version > 0);
    }

    [Fact]
    public void Set_ChangesVersion() {
        _store.Set("key", "value1");

        _store.TryGet(
            "key",
            out var first);

        _store.Set("key", "value2");

        _store.TryGet(
            "key",
            out var second);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(
            second.Version > first.Version);
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue() {
        _store.Set("key", "value");

        bool removed = _store.Remove("key");

        Assert.True(removed);
        Assert.False(_store.Contains("key"));
    }

    [Fact]
    public void Remove_MissingKey_ReturnsFalse() {
        bool removed = _store.Remove("missing");

        Assert.False(removed);
    }

    [Fact]
    public void Contains_ExistingKey_ReturnsTrue() {
        _store.Set("key", "value");

        Assert.True(
            _store.Contains("key"));
    }

    [Fact]
    public void Contains_MissingKey_ReturnsFalse() {
        Assert.False(
            _store.Contains("missing"));
    }

    [Fact]
    public void Increment_MissingKey_CreatesValue() {
        long result = _store.Increment("counter");

        Assert.Equal(1, result);

        _store.TryGet(
            "counter",
            out var value);

        Assert.NotNull(value);
        Assert.Equal("1", value.Value);
    }

    [Fact]
    public void Increment_ExistingInteger_IncrementsValue() {
        _store.Set("counter", "10");

        long result =
            _store.Increment("counter");

        Assert.Equal(11, result);
    }

    [Fact]
    public void Increment_WithAmount_IncrementsByAmount() {
        _store.Set("counter", "10");

        long result =
            _store.Increment("counter", 5);

        Assert.Equal(15, result);
    }

    [Fact]
    public void Increment_WithNegativeAmount_DecrementsValue() {
        _store.Set("counter", "10");

        long result =
            _store.Increment("counter", -3);

        Assert.Equal(7, result);
    }

    [Fact]
    public void Increment_NonIntegerValue_Throws() {
        _store.Set("key", "hello");

        Assert.Throws<InvalidOperationException>(
            () => _store.Increment("key"));
    }

    [Fact]
    public void Increment_Overflow_Throws() {
        _store.Set(
            "counter",
            long.MaxValue.ToString());

        Assert.Throws<InvalidOperationException>(
            () => _store.Increment("counter"));
    }

    [Fact]
    public async Task ConcurrentIncrement_ProducesCorrectResult() {
        const int operations = 1_000;

        var tasks = Enumerable
            .Range(0, operations)
            .Select(
                _ => Task.Run(
                    () => _store.Increment("counter")))
            .ToArray();

        await Task.WhenAll(tasks);

        _store.TryGet(
            "counter",
            out var value);

        Assert.NotNull(value);
        Assert.Equal(
            operations.ToString(),
            value.Value);
    }

    [Fact]
    public void Set_NullKey_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => _store.Set(null!, "value"));
    }

    [Fact]
    public void Set_EmptyKey_Throws() {
        Assert.Throws<ArgumentException>(
            () => _store.Set("", "value"));
    }

    [Fact]
    public void Set_NullValue_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => _store.Set("key", null!));
    }

    [Fact]
    public void Get_NullKey_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => _store.TryGet(
                null!,
                out _));
    }

    [Fact]
    public void Remove_NullKey_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => _store.Remove(null!));
    }

    [Fact]
    public void Contains_NullKey_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => _store.Contains(null!));
    }

    [Fact]
    public void Increment_NullKey_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => _store.Increment(null!));
    }

    [Fact]
    public void Set_WithFutureExpiration_ValueRemainsAccessible() {
        DateTimeOffset expiration =
            _clock.UtcNow.AddSeconds(10);

        _store.Set(
            "key",
            "value",
            expiration);

        bool exists =
            _store.TryGet(
                "key",
                out var storedValue);

        Assert.True(exists);
        Assert.NotNull(storedValue);
        Assert.Equal("value", storedValue.Value);
        Assert.Equal(
            expiration,
            storedValue.ExpiresAt);
    }

    [Fact]
    public void Set_WhenExpirationReached_ValueIsExpired() {
        DateTimeOffset expiration =
            _clock.UtcNow.AddSeconds(10);

        _store.Set(
            "key",
            "value",
            expiration);

        _clock.Advance(
            TimeSpan.FromSeconds(10));

        bool exists =
            _store.TryGet(
                "key",
                out var storedValue);

        Assert.False(exists);
        Assert.Null(storedValue);
    }

    [Fact]
    public void Set_WhenExpirationPassed_ValueIsExpired() {
        DateTimeOffset expiration =
            _clock.UtcNow.AddSeconds(10);

        _store.Set(
            "key",
            "value",
            expiration);

        _clock.Advance(
            TimeSpan.FromSeconds(11));

        bool exists =
            _store.Contains("key");

        Assert.False(exists);
    }

    [Fact]
    public void Set_WithoutExpiration_ValueDoesNotExpire() {
        _store.Set(
            "key",
            "value");

        _clock.Advance(
            TimeSpan.FromDays(365));

        bool exists =
            _store.TryGet(
                "key",
                out var storedValue);

        Assert.True(exists);
        Assert.NotNull(storedValue);
        Assert.Equal("value", storedValue.Value);
        Assert.Null(storedValue.ExpiresAt);
    }

    [Fact]
    public void Set_ReplacingExpiringValue_RemovesPreviousExpiration() {
        DateTimeOffset expiration =
            _clock.UtcNow.AddSeconds(10);

        _store.Set(
            "key",
            "value",
            expiration);

        _store.Set(
            "key",
            "new-value");

        _clock.Advance(
            TimeSpan.FromSeconds(20));

        bool exists =
            _store.TryGet(
                "key",
                out var storedValue);

        Assert.True(exists);
        Assert.NotNull(storedValue);
        Assert.Equal(
            "new-value",
            storedValue.Value);
        Assert.Null(storedValue.ExpiresAt);
    }

    [Fact]
    public void Increment_OnExpiredValue_CreatesNewPersistentValue() {
        DateTimeOffset expiration =
            _clock.UtcNow.AddSeconds(10);

        _store.Set(
            "counter",
            "10",
            expiration);

        _clock.Advance(
            TimeSpan.FromSeconds(10));

        long result =
            _store.Increment(
                "counter");

        Assert.Equal(
            1,
            result);

        bool exists =
            _store.TryGet(
                "counter",
                out var storedValue);

        Assert.True(exists);
        Assert.NotNull(storedValue);
        Assert.Equal(
            "1",
            storedValue.Value);
        Assert.Null(storedValue.ExpiresAt);
    }
}