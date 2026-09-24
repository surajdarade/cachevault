using System.Text;
using CacheVault.Core.Eviction.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Eviction;

public sealed class EvictionManager : IEvictionManager
{
    private const long EntryOverheadBytes = 64;
    private static readonly TimeSpan LfuDecayInterval =
        TimeSpan.FromMinutes(1);

    private readonly object _gate = new();

    private readonly Dictionary<string, EvictionEntry> _entries =
        new(StringComparer.Ordinal);

    private readonly Random _random = new();

    private long _memoryUsageBytes;
    private long _accessSequence;

    public EvictionManager(
        long maxMemoryBytes,
        EvictionPolicy policy)
    {
        if (maxMemoryBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMemoryBytes),
                "Maximum memory cannot be negative.");
        }

        if (maxMemoryBytes == 0)
        {
            policy = EvictionPolicy.NoEviction;
        }

        MaxMemoryBytes = maxMemoryBytes;
        Policy = policy;
    }

    public long MemoryUsageBytes
    {
        get
        {
            lock (_gate)
            {
                return _memoryUsageBytes;
            }
        }
    }

    public long MaxMemoryBytes { get; }

    public EvictionPolicy Policy { get; }

    public void OnSet(
        string key,
        StoredValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        OnSet(
            key,
            CalculateMemoryUsage(
                key,
                value.Value),
            isList: false);
    }

    public void OnSet(
        string key,
        long memoryUsageBytes,
        bool isList)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (memoryUsageBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryUsageBytes));
        }

        lock (_gate)
        {
            if (_entries.TryGetValue(
                    key,
                    out EvictionEntry? existing))
            {
                _memoryUsageBytes -=
                    existing.MemoryUsageBytes;
            }

            _entries[key] =
                new EvictionEntry(
                    key,
                    memoryUsageBytes,
                    NextAccessSequence(),
                    isList);

            _memoryUsageBytes +=
                memoryUsageBytes;
        }
    }

    public void OnAccess(
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            if (!_entries.TryGetValue(
                    key,
                    out EvictionEntry? entry))
            {
                return;
            }

            entry.LastAccessSequence =
                NextAccessSequence();

            ApplyLfuDecay(entry);

            if (entry.Frequency < byte.MaxValue)
            {
                entry.Frequency++;
            }
        }
    }

    public void OnRemove(
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            if (!_entries.Remove(
                    key,
                    out EvictionEntry? entry))
            {
                return;
            }

            _memoryUsageBytes -=
                entry.MemoryUsageBytes;
        }
    }

    public bool TrySelectCandidate(
        out string? key)
    {
        return TrySelectCandidate(
            out key,
            out _);
    }

    public bool TrySelectCandidate(
        out string? key,
        out bool isList)
    {
        lock (_gate)
        {
            if (Policy == EvictionPolicy.NoEviction ||
                _entries.Count == 0)
            {
                key = null;
                isList = false;

                return false;
            }

            EvictionEntry? selected =
                Policy switch
                {
                    EvictionPolicy.Lru =>
                        SelectLruCandidate(),
                    EvictionPolicy.Lfu =>
                        SelectLfuCandidate(),
                    EvictionPolicy.Random =>
                        SelectRandomCandidate(),
                    _ => null
                };

            if (selected is null)
            {
                key = null;
                isList = false;

                return false;
            }

            key = selected.Key;
            isList = selected.IsList;

            return true;
        }
    }

    private EvictionEntry? SelectLruCandidate()
    {
        EvictionEntry? candidate = null;

        foreach (EvictionEntry entry in _entries.Values)
        {
            if (candidate is null ||
                entry.LastAccessSequence <
                candidate.LastAccessSequence)
            {
                candidate = entry;
            }
        }

        return candidate;
    }

    private EvictionEntry? SelectLfuCandidate()
    {
        EvictionEntry? candidate = null;

        foreach (EvictionEntry entry in _entries.Values)
        {
            if (candidate is null ||
                entry.Frequency < candidate.Frequency ||
                entry.Frequency == candidate.Frequency &&
                entry.LastAccessSequence <
                candidate.LastAccessSequence)
            {
                candidate = entry;
            }
        }

        return candidate;
    }

    private EvictionEntry? SelectRandomCandidate()
    {
        int index =
            _random.Next(
                _entries.Count);

        foreach (EvictionEntry entry in _entries.Values)
        {
            if (index-- == 0)
            {
                return entry;
            }
        }

        return null;
    }

    private void ApplyLfuDecay(
        EvictionEntry entry)
    {
        if (Policy != EvictionPolicy.Lfu)
        {
            return;
        }

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        if (now - entry.LastDecayUtc <
            LfuDecayInterval)
        {
            return;
        }

        TimeSpan elapsed =
            now - entry.LastDecayUtc;

        int periods =
            Math.Max(
                1,
                (int)(elapsed.Ticks /
                    LfuDecayInterval.Ticks));

        for (int index = 0;
             index < periods;
             index++)
        {
            entry.Frequency =
                (byte)((entry.Frequency + 1) / 2);
        }

        entry.LastDecayUtc = now;
    }

    private long NextAccessSequence()
    {
        return ++_accessSequence;
    }

    private static long CalculateMemoryUsage(
        string key,
        string value)
    {
        long keyBytes =
            Encoding.UTF8.GetByteCount(key);

        long valueBytes =
            Encoding.UTF8.GetByteCount(value);

        return checked(
            EntryOverheadBytes +
            keyBytes +
            valueBytes);
    }

    private sealed class EvictionEntry
    {
        public EvictionEntry(
            string key,
            long memoryUsageBytes,
            long lastAccessSequence,
            bool isList)
        {
            Key = key;
            MemoryUsageBytes = memoryUsageBytes;
            LastAccessSequence = lastAccessSequence;
            Frequency = 1;
            IsList = isList;
            LastDecayUtc = DateTimeOffset.UtcNow;
        }

        public string Key { get; set; } = string.Empty;

        public long MemoryUsageBytes { get; set; }

        public long LastAccessSequence { get; set; }

        public byte Frequency { get; set; }

        public bool IsList { get; }

        public DateTimeOffset LastDecayUtc { get; set; }
    }
}
