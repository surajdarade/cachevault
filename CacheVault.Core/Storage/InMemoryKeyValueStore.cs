using System.Collections.Concurrent;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Eviction.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Storage;

public sealed class InMemoryKeyValueStore :
    IKeyValueStore,
    IKeyValueStoreSnapshot,
    IResettableKeyValueStore {
    private readonly ConcurrentDictionary<string, StoredValue> _values =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, long> _keyVersions =
        new(StringComparer.Ordinal);

    private readonly IClock _clock;
    private readonly IEvictionManager _evictionManager;
    private readonly IEvictionCoordinator? _evictionCoordinator;

    private long _version;

    public InMemoryKeyValueStore(
        IClock clock,
        IEvictionManager? evictionManager = null,
        IEvictionCoordinator? evictionCoordinator = null) {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _evictionManager =
            evictionManager ??
            new CacheVault.Core.Eviction.EvictionManager(
                0,
                EvictionPolicy.NoEviction);

        _evictionCoordinator =
            evictionCoordinator;

        _evictionCoordinator?.RegisterStringKeyspace(
            RemoveForEviction);
    }

    public void Clear()
    {
        foreach (string key in _values.Keys.ToArray())
        {
            Remove(key);
        }
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
            RemoveExpiredValue(
                key,
                storedValue);

            value = null;

            return false;
        }

        _evictionManager.OnAccess(key);

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
                expiresAt) {
                Version = version
            };

        _values[key] =
            storedValue;

        _keyVersions[key] =
            version;

        _evictionManager.OnSet(
            key,
            storedValue);

        EnforceMemoryLimit();
    }

    public bool Remove(
        string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_values.TryRemove(
                key,
                out _)) {
            return false;
        }

        MarkKeyChanged(key);
        _evictionManager.OnRemove(key);

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
                        amount.ToString()) {
                        Version = version
                    };

                if (_values.TryAdd(
                        key,
                        createdValue)) {
                    _keyVersions[key] =
                        version;

                    _evictionManager.OnSet(
                        key,
                        createdValue);

                    EnforceMemoryLimit();

                    return amount;
                }

                continue;
            }

            if (IsExpired(currentValue)) {
                RemoveExpiredValue(
                    key,
                    currentValue);

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
                    currentValue.ExpiresAt) {
                    Version = updatedVersion
                };

            if (_values.TryUpdate(
                    key,
                    updatedValue,
                    currentValue)) {
                _keyVersions[key] =
                    updatedVersion;

                _evictionManager.OnSet(
                    key,
                    updatedValue);

                EnforceMemoryLimit();

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
                RemoveExpiredValue(
                    entry.Key,
                    entry.Value);

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

    private void EnforceMemoryLimit()
    {
        if (_evictionCoordinator is not null)
        {
            _evictionCoordinator.EnforceMemoryLimit();
            return;
        }

        if (_evictionManager.MaxMemoryBytes <= 0)
        {
            return;
        }

        RemoveExpiredEntries();

        while (_evictionManager.MemoryUsageBytes >
               _evictionManager.MaxMemoryBytes)
        {
            if (!_evictionManager.TrySelectCandidate(
                    out string? candidate,
                    out bool isList) ||
                candidate is null)
            {
                return;
            }

            if (isList)
            {
                return;
            }

            RemoveForEviction(candidate);
            _evictionManager.OnRemove(candidate);
        }
    }

    private bool RemoveForEviction(string key)
    {
        if (!_values.TryRemove(
                key,
                out _))
        {
            return false;
        }

        MarkKeyChanged(key);

        return true;
    }

    private void RemoveExpiredEntries() {
        foreach (KeyValuePair<string, StoredValue> entry
            in _values) {
            if (IsExpired(entry.Value)) {
                RemoveExpiredValue(
                    entry.Key,
                    entry.Value);
            }
        }
    }

    private void RemoveExpiredValue(
        string key,
        StoredValue value) {
        if (!_values.TryRemove(
                new KeyValuePair<string, StoredValue>(
                    key,
                    value))) {
            return;
        }

        MarkKeyChanged(key);
        _evictionManager.OnRemove(key);
    }

    private void MarkKeyChanged(
        string key) {
        long version =
            Interlocked.Increment(
                ref _version);

        _keyVersions[key] =
            version;
    }

    private bool IsExpired(
        StoredValue value) {
        return value.ExpiresAt.HasValue &&
               value.ExpiresAt.Value <=
               _clock.UtcNow;
    }
}
