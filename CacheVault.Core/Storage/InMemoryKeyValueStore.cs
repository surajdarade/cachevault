using System.Collections.Concurrent;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStore : IKeyValueStore {
    private readonly ConcurrentDictionary<string, StoredValue> _values =
        new();

    private readonly IClock _clock;

    private long _version;

    public InMemoryKeyValueStore(
        IClock clock) {
        ArgumentNullException.ThrowIfNull(
            clock);

        _clock =
            clock;
    }

    public bool TryGet(
        string key,
        out StoredValue? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        if (!_values.TryGetValue(
                key,
                out StoredValue? storedValue)) {
            value = null;

            return false;
        }

        if (IsExpired(storedValue)) {
            _values.TryRemove(
                new KeyValuePair<string, StoredValue>(
                    key,
                    storedValue));

            value = null;

            return false;
        }

        value =
            storedValue;

        return true;
    }

    public void Set(
        string key,
        string value,
        DateTimeOffset? expiresAt = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        ArgumentNullException.ThrowIfNull(
            value);

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
    }

    public bool Remove(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        return _values.TryRemove(
            key,
            out _);
    }

    public bool Contains(
        string key) {
        return TryGet(
            key,
            out _);
    }

    public long Increment(
        string key,
        long amount = 1) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        while (true) {
            if (!_values.TryGetValue(
                    key,
                    out StoredValue? current)) {
                long initialValue;

                try {
                    initialValue =
                        checked(amount);
                }
                catch (OverflowException exception) {
                    throw new InvalidOperationException(
                        "Increment would overflow the integer range.",
                        exception);
                }

                long version =
                    Interlocked.Increment(
                        ref _version);

                var newStoredValue =
                    new StoredValue(
                        initialValue.ToString())
                    {
                        Version = version
                    };

                if (_values.TryAdd(
                        key,
                        newStoredValue)) {
                    return initialValue;
                }

                continue;
            }

            if (IsExpired(current)) {
                _values.TryRemove(
                    new KeyValuePair<string, StoredValue>(
                        key,
                        current));

                continue;
            }

            if (!long.TryParse(
                    current.Value,
                    out long currentValue)) {
                throw new InvalidOperationException(
                    "Value is not an integer.");
            }

            long updatedValue;

            try {
                updatedValue =
                    checked(
                        currentValue + amount);
            }
            catch (OverflowException exception) {
                throw new InvalidOperationException(
                    "Increment would overflow the integer range.",
                    exception);
            }

            var updatedStoredValue =
                new StoredValue(
                    updatedValue.ToString())
                {
                    Version =
                        Interlocked.Increment(
                            ref _version)
                };

            if (_values.TryUpdate(
                    key,
                    updatedStoredValue,
                    current)) {
                return updatedValue;
            }
        }
    }

    private bool IsExpired(
        StoredValue value) {
        return value.ExpiresAt.HasValue &&
               value.ExpiresAt.Value <= _clock.UtcNow;
    }
}