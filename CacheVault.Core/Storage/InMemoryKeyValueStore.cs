using System.Collections.Concurrent;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStore :
    IKeyValueStore,
    IKeyValueStoreSnapshot {
    private readonly ConcurrentDictionary<string, StoredValue> _values =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, long> _keyVersions =
        new(StringComparer.Ordinal);

    private readonly IClock _clock;

    private long _version;

    public InMemoryKeyValueStore(
        IClock clock) {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
    }

    public bool TryGet(
        string key,
        out StoredValue? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_values.TryGetValue(
                key,
                out StoredValue? storedValue)) {
            value = null;

            return false;
        }

        if (IsExpired(storedValue)) {
            if (_values.TryRemove(
                    new KeyValuePair<string, StoredValue>(
                        key,
                        storedValue))) {
                long expirationVersion =
                    Interlocked.Increment(
                        ref _version);

                _keyVersions[key] =
                    expirationVersion;
            }

            value = null;

            return false;
        }

        value = storedValue;

        return true;
    }

    public void Set(
        string key,
        string value,
        DateTimeOffset? expiresAt = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        long version =
            Interlocked.Increment(
                ref _version);

        var storedValue =
            new StoredValue(
                value,
                expiresAt)
            {
                Version = version
            };

        _values[key] =
            storedValue;

        _keyVersions[key] =
            version;
    }

    public bool Remove(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_values.TryRemove(
                key,
                out _)) {
            return false;
        }

        long version =
            Interlocked.Increment(
                ref _version);

        _keyVersions[key] =
            version;

        return true;
    }

    public bool Contains(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return TryGet(
            key,
            out _);
    }

    public long Increment(
        string key,
        long amount = 1) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        while (true) {
            if (!_values.TryGetValue(
                    key,
                    out StoredValue? currentValue)) {
                long version =
                    Interlocked.Increment(
                        ref _version);

                var createdValue =
                    new StoredValue(
                        amount.ToString())
                    {
                        Version = version
                    };

                if (_values.TryAdd(
                        key,
                        createdValue)) {
                    _keyVersions[key] =
                        version;

                    return amount;
                }

                continue;
            }

            if (IsExpired(currentValue)) {
                if (_values.TryRemove(
                        new KeyValuePair<string, StoredValue>(
                            key,
                            currentValue))) {
                    long expirationVersion =
                        Interlocked.Increment(
                            ref _version);

                    _keyVersions[key] =
                        expirationVersion;
                }

                continue;
            }

            if (!long.TryParse(
                    currentValue.Value,
                    out long currentNumber)) {
                throw new InvalidOperationException(
                    "Value is not an integer or is out of range.");
            }

            long newNumber;

            try {
                newNumber =
                    checked(
                        currentNumber + amount);
            }
            catch (OverflowException) {
                throw new InvalidOperationException(
                    "Value is not an integer or is out of range.");
            }

            long updatedVersion =
                Interlocked.Increment(
                    ref _version);

            var updatedValue =
                new StoredValue(
                    newNumber.ToString(),
                    currentValue.ExpiresAt)
                {
                    Version = updatedVersion
                };

            if (_values.TryUpdate(
                    key,
                    updatedValue,
                    currentValue)) {
                _keyVersions[key] =
                    updatedVersion;

                return newNumber;
            }
        }
    }

    public long GetVersion(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        TryGet(
            key,
            out _);

        return _keyVersions.TryGetValue(
            key,
            out long version)
            ? version
            : 0;
    }

    public IReadOnlyList<PersistentKeyValue> GetSnapshot() {
        var snapshot =
            new List<PersistentKeyValue>(
                _values.Count);

        foreach (KeyValuePair<string, StoredValue> entry
            in _values) {
            if (IsExpired(entry.Value)) {
                if (_values.TryRemove(
                        new KeyValuePair<string, StoredValue>(
                            entry.Key,
                            entry.Value))) {
                    long expirationVersion =
                        Interlocked.Increment(
                            ref _version);

                    _keyVersions[entry.Key] =
                        expirationVersion;
                }

                continue;
            }

            snapshot.Add(
                new PersistentKeyValue(
                    entry.Key,
                    entry.Value.Value,
                    entry.Value.ExpiresAt));
        }

        return snapshot;
    }

    private bool IsExpired(
        StoredValue value) {
        return value.ExpiresAt.HasValue &&
               value.ExpiresAt.Value <=
               _clock.UtcNow;
    }
}