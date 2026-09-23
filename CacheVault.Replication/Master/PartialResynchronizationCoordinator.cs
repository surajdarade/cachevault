using System.Text;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class PartialResynchronizationCoordinator :
    IPartialResynchronizationCoordinator {
    private readonly ReplicaConnection _connection;
    private readonly IReplicationBacklog _backlog;

    public PartialResynchronizationCoordinator(
        ReplicaConnection connection,
        IReplicationBacklog backlog) {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            backlog);

        _connection = connection;
        _backlog = backlog;
    }

    public async ValueTask ExecuteAsync(
        long requestedOffset,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        if (requestedOffset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(requestedOffset));
        }

        if (_connection.State !=
            ReplicaConnectionState.Synchronizing) {
            throw new InvalidOperationException(
                "Partial resynchronization requires a synchronizing replica connection.");
        }

        if (!_backlog.TryReadFrom(
                requestedOffset,
                out byte[] data)) {
            throw new InvalidOperationException(
                "Replication backlog no longer contains the requested synchronization offset.");
        }

        await SendContinueAsync(
            cancellationToken);

        if (data.Length > 0) {
            await _connection.WriteAsync(
                data,
                cancellationToken);
        }

        _connection.MarkOnline();
    }

    private async ValueTask SendContinueAsync(
        CancellationToken cancellationToken) {
        byte[] response =
            Encoding.UTF8.GetBytes(
                "+CONTINUE\r\n");

        await _connection.WriteAsync(
            response,
            cancellationToken);
    }
}