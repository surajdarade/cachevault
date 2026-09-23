using CacheVault.Replication.Master;

namespace CacheVault.Replication.Abstractions;

public interface IReplicaConnectionRegistry {
    int Count { get; }

    void Register(
        ReplicaConnection connection);

    bool Remove(
        string replicaId);

    bool TryGet(
        string replicaId,
        out ReplicaConnection? connection);

    IReadOnlyList<ReplicaConnection> GetAll();
}