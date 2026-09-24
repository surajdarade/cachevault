using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicationManager :
    IReplicationManager,
    IAsyncDisposable {
    private readonly IReplicationState _state;
    private readonly IReplicationCommandEncoder _encoder;
    private readonly IReplicationBacklog _backlog;
    private readonly IReplicationBroadcaster _broadcaster;
    private readonly SemaphoreSlim _publicationLock =
        new(1, 1);

    private bool _disposed;

    public ReplicationManager(
        IReplicationState state,
        IReplicationCommandEncoder encoder,
        IReplicationBacklog backlog,
        IReplicationBroadcaster broadcaster) {
        ArgumentNullException.ThrowIfNull(
            state);

        ArgumentNullException.ThrowIfNull(
            encoder);

        ArgumentNullException.ThrowIfNull(
            backlog);

        ArgumentNullException.ThrowIfNull(
            broadcaster);

        _state = state;
        _encoder = encoder;
        _backlog = backlog;
        _broadcaster = broadcaster;
    }

    public long ReplicationOffset =>
        _state.ReplicationOffset;

    public async ValueTask ReplicateAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        cancellationToken.ThrowIfCancellationRequested();

        await _publicationLock.WaitAsync(
            cancellationToken);

        try {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            long startOffset =
                _state.ReplicationOffset;

            ReplicationEntry entry =
                _encoder.Encode(
                    startOffset,
                    commandName,
                    arguments);

            _backlog.Append(
                entry);

            _state.AdvanceReplicationOffset(
                entry.Length);

            await _broadcaster.BroadcastAsync(
                entry,
                cancellationToken);
        }
        finally {
            _publicationLock.Release();
        }
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        _publicationLock.Dispose();

        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}