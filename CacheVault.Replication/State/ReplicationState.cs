using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.State;

public sealed class ReplicationState : IReplicationState {
    private long _replicationOffset;
    private long _acknowledgedOffset;

    public ReplicationState(
        bool isMaster = true,
        string? replicationId = null) {
        IsMaster = isMaster;

        ReplicationId =
            string.IsNullOrWhiteSpace(replicationId)
                ? Guid.NewGuid().ToString("N")
                : replicationId;

        _replicationOffset = 0;
        _acknowledgedOffset = 0;
    }

    public string ReplicationId { get; }

    public long ReplicationOffset =>
        Interlocked.Read(
            ref _replicationOffset);

    public long AcknowledgedOffset =>
        Interlocked.Read(
            ref _acknowledgedOffset);

    public bool IsMaster { get; }

    public void AdvanceReplicationOffset(
        long bytes) {
        if (bytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(bytes),
                "Replication offset increment must be greater than zero.");
        }

        Interlocked.Add(
            ref _replicationOffset,
            bytes);
    }

    public void Acknowledge(
        long offset) {
        if (offset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Acknowledged offset cannot be negative.");
        }

        while (true) {
            long current =
                Interlocked.Read(
                    ref _acknowledgedOffset);

            if (offset <= current) {
                return;
            }

            long previous =
                Interlocked.CompareExchange(
                    ref _acknowledgedOffset,
                    offset,
                    current);

            if (previous == current) {
                return;
            }
        }
    }
}