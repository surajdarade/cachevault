namespace CacheVault.Replication.Protocol;

public sealed record PsyncCommand(
    string ReplicationId,
    long Offset);