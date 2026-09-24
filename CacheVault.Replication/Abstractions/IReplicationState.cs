namespace CacheVault.Replication.Abstractions;

public interface IReplicationState {
    string ReplicationId { get; }

    long ReplicationOffset { get; }

    long AcknowledgedOffset { get; }

    bool IsMaster { get; }

    void AdvanceReplicationOffset(long bytes);

    void SetReplicationState(
        string replicationId,
        long replicationOffset);

    void Acknowledge(long offset);
}