using System.Net.Sockets;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicationWaiter :
    IReplicationWaiter {
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(10);

    private readonly IReplicationState _replicationState;

    private readonly IReplicaConnectionRegistry
        _connectionRegistry;

    private readonly IReplicationProtocolEncoder
        _protocolEncoder;

    public ReplicationWaiter(
        IReplicationState replicationState,
        IReplicaConnectionRegistry connectionRegistry,
        IReplicationProtocolEncoder protocolEncoder) {
        ArgumentNullException.ThrowIfNull(
            replicationState);

        ArgumentNullException.ThrowIfNull(
            connectionRegistry);

        ArgumentNullException.ThrowIfNull(
            protocolEncoder);

        _replicationState =
            replicationState;

        _connectionRegistry =
            connectionRegistry;

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

        IReadOnlyList<ReplicaConnection> connections =
            GetOnlineConnections();

        if (connections.Count == 0) {
            return 0;
        }

        await RequestAcknowledgementsAsync(
            connections,
            cancellationToken);

        long acknowledgedCount =
            CountAcknowledgedReplicas(
                connections,
                targetOffset);

        if (acknowledgedCount >= replicaCount) {
            return acknowledgedCount;
        }

        if (timeout == TimeSpan.Zero) {
            return acknowledgedCount;
        }

        DateTimeOffset deadline =
            DateTimeOffset.UtcNow.Add(
                timeout);

        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();

            connections =
                GetOnlineConnections();

            acknowledgedCount =
                CountAcknowledgedReplicas(
                    connections,
                    targetOffset);

            if (acknowledgedCount >= replicaCount) {
                return acknowledgedCount;
            }

            TimeSpan remaining =
                deadline -
                DateTimeOffset.UtcNow;

            if (remaining <= TimeSpan.Zero) {
                break;
            }

            TimeSpan delay =
                remaining < PollInterval
                    ? remaining
                    : PollInterval;

            await Task.Delay(
                delay,
                cancellationToken);
        }

        return CountAcknowledgedReplicas(
            GetOnlineConnections(),
            targetOffset);
    }

    private IReadOnlyList<ReplicaConnection>
        GetOnlineConnections() {
        return _connectionRegistry
            .GetAll()
            .Where(
                connection =>
                    connection.State ==
                    ReplicaConnectionState.Online)
            .ToArray();
    }

    private async Task RequestAcknowledgementsAsync(
        IReadOnlyList<ReplicaConnection> connections,
        CancellationToken cancellationToken) {
        byte[] request =
            _protocolEncoder.EncodeReplConf(
                new ReplConfCommand(
                    "GETACK",
                    ["*"]));

        List<Task> tasks =
            [];

        foreach (ReplicaConnection connection in connections) {
            tasks.Add(
                SendGetAcknowledgementAsync(
                    connection,
                    request,
                    cancellationToken));
        }

        if (tasks.Count == 0) {
            return;
        }

        await Task.WhenAll(
            tasks);
    }

    private static async Task SendGetAcknowledgementAsync(
        ReplicaConnection connection,
        byte[] request,
        CancellationToken cancellationToken) {
        try {
            await connection.WriteAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (IOException) {
        }
        catch (SocketException) {
        }
        catch (ObjectDisposedException) {
        }
    }

    private static long CountAcknowledgedReplicas(
        IReadOnlyList<ReplicaConnection> connections,
        long targetOffset) {
        long count = 0;

        foreach (ReplicaConnection connection in connections) {
            if (connection.State !=
                ReplicaConnectionState.Online) {
                continue;
            }

            if (connection.Replica.AcknowledgedOffset >=
                targetOffset) {
                count++;
            }
        }

        return count;
    }
}