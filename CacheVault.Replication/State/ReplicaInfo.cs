namespace CacheVault.Replication.State;

public sealed class ReplicaInfo {
    public ReplicaInfo(
        string replicaId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replicaId);

        ReplicaId =
            replicaId;
    }

    public string ReplicaId { get; }

    public long AcknowledgedOffset { get; private set; }

    public void Acknowledge(
        long offset) {
        if (offset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Acknowledged offset cannot be negative.");
        }

        if (offset <= AcknowledgedOffset) {
            return;
        }

        AcknowledgedOffset =
            offset;
    }
}