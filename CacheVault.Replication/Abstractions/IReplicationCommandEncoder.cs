using CacheVault.Replication.State;

namespace CacheVault.Replication.Abstractions;

public interface IReplicationCommandEncoder {
    ReplicationEntry Encode(
        long startOffset,
        string commandName,
        IReadOnlyList<string> arguments);
}