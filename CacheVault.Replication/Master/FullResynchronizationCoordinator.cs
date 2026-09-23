using System.Text;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class FullResynchronizationCoordinator :
    IFullResynchronizationCoordinator {
    private readonly ReplicaConnection _connection;
    private readonly IReplicationSnapshotProvider _snapshotProvider;
    private readonly IReplicationState _replicationState;
    private readonly IReplicationBacklog _backlog;

    public FullResynchronizationCoordinator(
        ReplicaConnection connection,
        IReplicationSnapshotProvider snapshotProvider,
        IReplicationState replicationState,
        IReplicationBacklog backlog) {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            snapshotProvider);

        ArgumentNullException.ThrowIfNull(
            replicationState);

        ArgumentNullException.ThrowIfNull(
            backlog);

        _connection = connection;
        _snapshotProvider =
            snapshotProvider;
        _replicationState =
            replicationState;
        _backlog =
            backlog;
    }

    public async ValueTask ExecuteAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connection.State !=
            ReplicaConnectionState.Synchronizing) {
            throw new InvalidOperationException(
                "Full resynchronization requires a synchronizing replica connection.");
        }

        FullResynchronizationContext context =
            CaptureSynchronizationContext();

        await SendFullResyncHeaderAsync(
            context,
            cancellationToken);

        await using var snapshotStream =
            new MemoryStream();

        await _snapshotProvider.WriteSnapshotAsync(
            snapshotStream,
            cancellationToken);

        snapshotStream.Position = 0;

        await SendSnapshotAsync(
            snapshotStream,
            cancellationToken);

        await SendBacklogCatchUpAsync(
            context,
            cancellationToken);

        _connection.MarkOnline();
    }

    private FullResynchronizationContext
        CaptureSynchronizationContext() {
        return new FullResynchronizationContext(
            _replicationState.ReplicationId,
            _replicationState.ReplicationOffset);
    }

    private async ValueTask SendFullResyncHeaderAsync(
        FullResynchronizationContext context,
        CancellationToken cancellationToken) {
        string response =
            $"FULLRESYNC " +
            $"{context.ReplicationId} " +
            $"{context.ReplicationOffset}";

        byte[] data =
            Encoding.UTF8.GetBytes(
                $"+{response}\r\n");

        await _connection.WriteAsync(
            data,
            cancellationToken);
    }

    private async ValueTask SendSnapshotAsync(
        Stream snapshot,
        CancellationToken cancellationToken) {
        byte[] buffer =
            new byte[81920];

        while (true) {
            int bytesRead =
                await snapshot.ReadAsync(
                    buffer,
                    cancellationToken);

            if (bytesRead == 0) {
                break;
            }

            await _connection.WriteAsync(
                buffer.AsMemory(
                    0,
                    bytesRead),
                cancellationToken);
        }
    }

    private async ValueTask SendBacklogCatchUpAsync(
        FullResynchronizationContext context,
        CancellationToken cancellationToken) {
        if (!_backlog.TryReadFrom(
                context.ReplicationOffset,
                out byte[] data)) {
            throw new InvalidOperationException(
                "Replication backlog no longer contains the FULLRESYNC synchronization offset.");
        }

        if (data.Length == 0) {
            return;
        }

        await _connection.WriteAsync(
            data,
            cancellationToken);
    }
}