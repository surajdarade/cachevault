using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicaRegistry :
    IReplicaRegistry {
    private readonly Dictionary<string, ReplicaInfo> _replicas =
        new(StringComparer.Ordinal);

    private readonly object _sync = new();

    public int Count {
        get {
            lock (_sync) {
                return _replicas.Count;
            }
        }
    }

    public void Register(
        ReplicaInfo replica) {
        ArgumentNullException.ThrowIfNull(
            replica);

        lock (_sync) {
            if (_replicas.ContainsKey(
                    replica.ReplicaId)) {
                throw new InvalidOperationException(
                    $"Replica '{replica.ReplicaId}' is already registered.");
            }

            _replicas.Add(
                replica.ReplicaId,
                replica);
        }
    }

    public bool Remove(
        string replicaId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replicaId);

        lock (_sync) {
            return _replicas.Remove(
                replicaId);
        }
    }

    public bool TryGet(
        string replicaId,
        out ReplicaInfo? replica) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replicaId);

        lock (_sync) {
            return _replicas.TryGetValue(
                replicaId,
                out replica);
        }
    }

    public IReadOnlyList<ReplicaInfo> GetAll() {
        lock (_sync) {
            return _replicas.Values.ToArray();
        }
    }
}