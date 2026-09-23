namespace CacheVault.Replication.Protocol;

public sealed record ReplicationAck(
    long Offset);