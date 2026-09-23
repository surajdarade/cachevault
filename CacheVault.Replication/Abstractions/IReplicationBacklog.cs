using CacheVault.Replication.State;

namespace CacheVault.Replication.Abstractions;

public interface IReplicationBacklog {
    long FirstOffset { get; }

    long EndOffset { get; }

    long Length { get; }

    long Capacity { get; }

    void Append(
        ReplicationEntry entry);

    bool TryReadFrom(
        long offset,
        out byte[] data);
}