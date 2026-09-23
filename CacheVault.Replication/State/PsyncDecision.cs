namespace CacheVault.Replication.State;

public sealed record PsyncDecision(
    bool RequiresFullResynchronization,
    long RequestedOffset,
    byte[] ReplicationData);