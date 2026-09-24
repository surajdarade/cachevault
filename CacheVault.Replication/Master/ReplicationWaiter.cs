using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicationWaiter :
    IReplicationWaiter {
    private readonly IReplicationState _replicationState;

    private readonly IReplicaConnectionRegistry
        _connectionRegistry;

    private readonly IReplicaRegistry _replicaRegistry;

    private readonly IReplicationProtocolEncoder
        _protocolEncoder;

    public ReplicationWaiter(
        IReplicationState replicationState,
        IReplicaConnectionRegistry connectionRegistry,
        IReplicaRegistry replicaRegistry,
        IReplicationProtocolEncoder protocolEncoder) {
        ArgumentNullException.ThrowIfNull(
            replicationState);

        ArgumentNullException.ThrowIfNull(
            connectionRegistry);

        ArgumentNullException.ThrowIfNull(
            replicaRegistry);

        ArgumentNullException.ThrowIfNull(
            protocolEncoder);

        _replicationState =
            replicationState;

        _connectionRegistry =
            connectionRegistry;

        _replicaRegistry =
            replicaRegistry;

        _protocolEncoder =
            protocolEncoder;
    }

    public async ValueTask<long> WaitAsync(
        int replicaCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        if (replicaCount < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(replicaCount),
                "Replica count cannot be negative.");
        }

        if (timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Timeout cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (replicaCount == 0) {
            return 0;
        }

        long targetOffset =
            _replicationState.ReplicationOffset;

        await RequestAcknowledgementsAsync(
            cancellationToken);

        if (CountAcknowledged(
                targetOffset) >= replicaCount) {
            return replicaCount;
        }

        if (timeout == TimeSpan.Zero) {
            return CountAcknowledged(
                targetOffset);
        }

        DateTimeOffset deadline =
            DateTimeOffset.UtcNow.Add(
                timeout);

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            long acknowledged =
                CountAcknowledged(
                    targetOffset);

            if (acknowledged >= replicaCount) {
                return replicaCount;
            }

            TimeSpan remaining =
                deadline -
                DateTimeOffset.UtcNow;

            if (remaining <= TimeSpan.Zero) {
                return acknowledged;
            }

            TimeSpan delay =
                remaining >
                TimeSpan.FromMilliseconds(10)
                    ? TimeSpan.FromMilliseconds(10)
                    : remaining;

            await Task.Delay(
                delay,
                cancellationToken);
        }
    }

    private async ValueTask RequestAcknowledgementsAsync(
        CancellationToken cancellationToken) {
        byte[] request =
            _protocolEncoder.EncodeReplConf(
                new ReplConfCommand(
                    "GETACK",
                    ["*"]));

        IReadOnlyList<ReplicaConnection> connections =
            _connectionRegistry.GetAll();

        List<Task> writes = [];

        foreach (ReplicaConnection connection in connections) {
            if (connection.State !=
                ReplicaConnectionState.Online) {
                continue;
            }

            writes.Add(
                connection.WriteAsync(
                    request,
                    cancellationToken)
                    .AsTask());
        }

        if (writes.Count == 0) {
            return;
        }

        await Task.WhenAll(
            writes);
    }

    private long CountAcknowledged(
        long targetOffset) {
        IReadOnlyList<ReplicaInfo> replicas =
            _replicaRegistry.GetAll();

        long count = 0;

        foreach (ReplicaInfo replica in replicas) {
            if (replica.AcknowledgedOffset >=
                targetOffset) {
                count++;
            }
        }

        return count;
    }
}