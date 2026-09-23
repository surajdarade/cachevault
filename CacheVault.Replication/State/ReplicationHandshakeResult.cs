namespace CacheVault.Replication.State;

public sealed record ReplicationHandshakeResult(
    ReplicationHandshakeState FinalState,
    string ReplicaId,
    long ReplicationOffset,
    bool RequiresFullResynchronization);