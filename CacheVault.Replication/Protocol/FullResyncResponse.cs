namespace CacheVault.Replication.Protocol;

public sealed record FullResyncResponse(
    string ReplicationId,
    long ReplicationOffset);