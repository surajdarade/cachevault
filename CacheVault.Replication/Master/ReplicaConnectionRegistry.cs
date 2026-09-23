using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.Master;

public sealed class ReplicaConnectionRegistry :
    IReplicaConnectionRegistry {
    private readonly Dictionary<string, ReplicaConnection> _connections =
        new(StringComparer.Ordinal);

    private readonly object _sync = new();

    public int Count {
        get {
            lock (_sync) {
                return _connections.Count;
            }
        }
    }

    public void Register(
        ReplicaConnection connection) {
        ArgumentNullException.ThrowIfNull(
            connection);

        string replicaId =
            connection.Replica.ReplicaId;

        lock (_sync) {
            if (_connections.ContainsKey(
                    replicaId)) {
                throw new InvalidOperationException(
                    $"Replica connection '{replicaId}' is already registered.");
            }

            _connections.Add(
                replicaId,
                connection);
        }
    }

    public bool Remove(
        string replicaId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replicaId);

        lock (_sync) {
            return _connections.Remove(
                replicaId);
        }
    }

    public bool TryGet(
        string replicaId,
        out ReplicaConnection? connection) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replicaId);

        lock (_sync) {
            return _connections.TryGetValue(
                replicaId,
                out connection);
        }
    }

    public IReadOnlyList<ReplicaConnection> GetAll() {
        lock (_sync) {
            return _connections.Values.ToArray();
        }
    }
}