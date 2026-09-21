using System.Collections.Concurrent;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStore : IKeyValueStore {
    private readonly ConcurrentDictionary<string, StoredValue> _store =
        new(StringComparer.Ordinal);

    private long _version;

    public bool TryGet(
        string key,
        out StoredValue? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _store.TryGetValue(
            key,
            out value);
    }

    public void Set(
        string key,
        string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        long version = Interlocked.Increment(
            ref _version);

        _store[key] = new StoredValue(value)
        {
            Version = version
        };
    }

    public bool Remove(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        bool removed = _store.TryRemove(
            key,
            out _);

        if (removed) {
            Interlocked.Increment(
                ref _version);
        }

        return removed;
    }

    public bool Contains(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _store.ContainsKey(key);
    }

    public long Increment(
        string key,
        long amount = 1) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        while (true) {
            if (!_store.TryGetValue(
                    key,
                    out StoredValue? existing)) {
                long newValue = amount;

                var created = new StoredValue(
                    newValue.ToString())
                {
                    Version = Interlocked.Increment(
                        ref _version)
                };

                if (_store.TryAdd(key, created)) {
                    return newValue;
                }

                continue;
            }

            if (!long.TryParse(
                    existing.Value,
                    out long currentValue)) {
                throw new InvalidOperationException(
                    "ERR value is not an integer or out of range");
            }

            long updatedValue;

            try {
                updatedValue = checked(
                    currentValue + amount);
            }
            catch (OverflowException) {
                throw new InvalidOperationException(
                    "ERR increment or decrement would overflow");
            }

            var replacement = new StoredValue(
                updatedValue.ToString())
            {
                Version = Interlocked.Increment(
                    ref _version)
            };

            if (_store.TryUpdate(
                    key,
                    replacement,
                    existing)) {
                return updatedValue;
            }
        }
    }
}