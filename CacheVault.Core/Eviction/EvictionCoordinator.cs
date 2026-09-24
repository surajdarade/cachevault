using CacheVault.Core.Eviction.Abstractions;
using CacheVault.Core.Models;

namespace CacheVault.Core.Eviction;

public sealed class EvictionCoordinator : IEvictionCoordinator
{
    private readonly IEvictionManager _manager;
    private readonly object _gate = new();

    private Func<string, bool>? _removeString;
    private Func<string, bool>? _removeList;

    public EvictionCoordinator(IEvictionManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        _manager = manager;
    }

    public void RegisterStringKeyspace(Func<string, bool> remove)
    {
        ArgumentNullException.ThrowIfNull(remove);
        lock (_gate)
        {
            _removeString = remove;
        }
    }

    public void RegisterListKeyspace(Func<string, bool> remove)
    {
        ArgumentNullException.ThrowIfNull(remove);
        lock (_gate)
        {
            _removeList = remove;
        }
    }

    public void EnforceMemoryLimit()
    {
        if (_manager.MaxMemoryBytes <= 0 ||
            _manager.Policy == EvictionPolicy.NoEviction)
        {
            return;
        }

        while (_manager.MemoryUsageBytes >
               _manager.MaxMemoryBytes)
        {
            if (!_manager.TrySelectCandidate(
                    out string? candidate,
                    out bool isList) ||
                candidate is null)
            {
                return;
            }

            Func<string, bool>? remover;

            lock (_gate)
            {
                remover = isList
                    ? _removeList
                    : _removeString;
            }

            if (remover is null ||
                !remover(candidate))
            {
                _manager.OnRemove(candidate);
            }
            else
            {
                _manager.OnRemove(candidate);
            }
        }
    }
}
