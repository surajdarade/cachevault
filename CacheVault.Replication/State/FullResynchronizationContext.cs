namespace CacheVault.Replication.State;

public sealed record FullResynchronizationContext(
    string ReplicationId,
    long ReplicationOffset);