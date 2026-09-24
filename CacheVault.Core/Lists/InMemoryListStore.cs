using System.Collections.Concurrent;
using System.Text;
using CacheVault.Core.Eviction.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Lists;

public sealed class InMemoryListStore :
    IListStore,
    IListStoreSnapshot,
    IResettableListStore
{
    private const long EntryOverheadBytes = 64;

    private readonly ConcurrentDictionary<string, List<string>> _lists =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, object> _locks =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, long> _versions =
        new(StringComparer.Ordinal);

    private long _version;

    private readonly IEvictionManager? _evictionManager;
    private readonly IEvictionCoordinator? _evictionCoordinator;

    public InMemoryListStore(
        IEvictionManager? evictionManager = null,
        IEvictionCoordinator? evictionCoordinator = null)
    {
        _evictionManager = evictionManager;
        _evictionCoordinator = evictionCoordinator;

        _evictionCoordinator?.RegisterListKeyspace(
            RemoveForEviction);
    }

    public int Length(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (GetLock(key))
        {
            if (!_lists.TryGetValue(
                    key,
                    out List<string>? list))
            {
                return 0;
            }

            _evictionManager?.OnAccess(key);

            return list.Count;
        }
    }

    public int PushLeft(
        string key,
        IReadOnlyList<string> values)
    {
        ValidateValues(key, values);

        lock (GetLock(key))
        {
            List<string> list =
                _lists.GetOrAdd(
                    key,
                    _ => []);

            foreach (string value in values)
            {
                list.Insert(0, value);
            }

            MarkKeyChanged(key);

            UpdateEvictionMetadata(
                key,
                list);

            _evictionCoordinator?.EnforceMemoryLimit();

            return list.Count;
        }
    }

    public int PushRight(
        string key,
        IReadOnlyList<string> values)
    {
        ValidateValues(key, values);

        lock (GetLock(key))
        {
            List<string> list =
                _lists.GetOrAdd(
                    key,
                    _ => []);

            list.AddRange(values);

            MarkKeyChanged(key);

            UpdateEvictionMetadata(
                key,
                list);

            _evictionCoordinator?.EnforceMemoryLimit();

            return list.Count;
        }
    }

    public string? PopLeft(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (GetLock(key))
        {
            if (!_lists.TryGetValue(
                    key,
                    out List<string>? list) ||
                list.Count == 0)
            {
                return null;
            }

            string value = list[0];
            list.RemoveAt(0);

            if (list.Count == 0)
            {
                RemoveInternal(
                    key,
                    notifyEviction: true);

                return value;
            }

            MarkKeyChanged(key);

            UpdateEvictionMetadata(
                key,
                list);

            return value;
        }
    }

    public string? PopRight(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (GetLock(key))
        {
            if (!_lists.TryGetValue(
                    key,
                    out List<string>? list) ||
                list.Count == 0)
            {
                return null;
            }

            int lastIndex = list.Count - 1;
            string value = list[lastIndex];

            list.RemoveAt(lastIndex);

            if (list.Count == 0)
            {
                RemoveInternal(
                    key,
                    notifyEviction: true);

                return value;
            }

            MarkKeyChanged(key);

            UpdateEvictionMetadata(
                key,
                list);

            return value;
        }
    }

    public IReadOnlyList<string> Range(
        string key,
        long start,
        long stop)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (GetLock(key))
        {
            if (!_lists.TryGetValue(
                    key,
                    out List<string>? list) ||
                list.Count == 0)
            {
                return [];
            }

            _evictionManager?.OnAccess(key);

            int count = list.Count;
            int normalizedStart =
                NormalizeIndex(
                    start,
                    count);

            int normalizedStop =
                NormalizeIndex(
                    stop,
                    count);

            if (normalizedStart < 0)
            {
                normalizedStart = 0;
            }

            if (normalizedStop < 0 ||
                normalizedStart >= count ||
                normalizedStart > normalizedStop)
            {
                return [];
            }

            if (normalizedStop >= count)
            {
                normalizedStop = count - 1;
            }

            int length =
                normalizedStop -
                normalizedStart +
                1;

            return list
                .GetRange(
                    normalizedStart,
                    length)
                .ToArray();
        }
    }

    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (GetLock(key))
        {
            return RemoveInternal(
                key,
                notifyEviction: true);
        }
    }

    public bool Contains(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (GetLock(key))
        {
            return _lists.ContainsKey(key);
        }
    }

    public long GetVersion(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _versions.TryGetValue(
            key,
            out long version)
            ? version
            : 0;
    }

    public IReadOnlyList<PersistentList> GetSnapshot()
    {
        var snapshot =
            new List<PersistentList>(
                _lists.Count);

        foreach (KeyValuePair<string, List<string>> entry
            in _lists)
        {
            lock (GetLock(entry.Key))
            {
                if (!_lists.TryGetValue(
                        entry.Key,
                        out List<string>? list))
                {
                    continue;
                }

                snapshot.Add(
                    new PersistentList(
                        entry.Key,
                        list.ToArray()));
            }
        }

        return snapshot;
    }

    public void Clear()
    {
        foreach (string key in _lists.Keys.ToArray())
        {
            Remove(key);
        }
    }

    private bool RemoveForEviction(string key)
    {
        lock (GetLock(key))
        {
            return RemoveInternal(
                key,
                notifyEviction: false);
        }
    }

    private bool RemoveInternal(
        string key,
        bool notifyEviction)
    {
        bool removed =
            _lists.TryRemove(
                key,
                out _);

        if (removed)
        {
            if (notifyEviction)
            {
                _evictionManager?.OnRemove(key);
            }

            MarkKeyChanged(key);
        }

        return removed;
    }

    private void UpdateEvictionMetadata(
        string key,
        List<string> list)
    {
        if (_evictionManager is null)
        {
            return;
        }

        _evictionManager.OnSet(
            key,
            CalculateMemoryUsage(
                key,
                list),
            isList: true);
    }

    private static long CalculateMemoryUsage(
        string key,
        IReadOnlyList<string> values)
    {
        long size =
            EntryOverheadBytes +
            Encoding.UTF8.GetByteCount(key);

        foreach (string value in values)
        {
            size = checked(
                size +
                Encoding.UTF8.GetByteCount(value));
        }

        return size;
    }

    private void MarkKeyChanged(string key)
    {
        _versions[key] =
            Interlocked.Increment(
                ref _version);
    }

    private object GetLock(string key)
    {
        return _locks.GetOrAdd(
            key,
            _ => new object());
    }

    private static void ValidateValues(
        string key,
        IReadOnlyList<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one list value is required.",
                nameof(values));
        }

        for (int index = 0;
             index < values.Count;
             index++)
        {
            ArgumentNullException.ThrowIfNull(
                values[index]);
        }
    }

    private static int NormalizeIndex(
        long index,
        int count)
    {
        if (index < 0)
        {
            long normalized =
                count + index;

            return normalized < int.MinValue
                ? int.MinValue
                : (int)normalized;
        }

        return index > int.MaxValue
            ? int.MaxValue
            : (int)index;
    }
}
