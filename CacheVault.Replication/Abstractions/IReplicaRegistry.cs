using CacheVault.Replication.State;

namespace CacheVault.Replication.Abstractions;

public interface IReplicaRegistry {
    int Count { get; }

    void Register(
        ReplicaInfo replica);

    bool Remove(
        string replicaId);

    bool TryGet(
        string replicaId,
        out ReplicaInfo? replica);

    IReadOnlyList<ReplicaInfo> GetAll();
}