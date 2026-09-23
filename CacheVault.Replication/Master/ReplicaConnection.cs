using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicaConnection :
    IAsyncDisposable {
    private readonly IReplicationTransport _transport;
    private readonly ReplicaInfo _replica;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _sync = new();

    private ReplicaConnectionState _state =
        ReplicaConnectionState.Disconnected;

    private bool _disposed;

    public ReplicaConnection(
        ReplicaInfo replica,
        IReplicationTransport transport) {
        ArgumentNullException.ThrowIfNull(replica);
        ArgumentNullException.ThrowIfNull(transport);

        _replica = replica;
        _transport = transport;
    }

    public ReplicaInfo Replica => _replica;

    public ReplicaConnectionState State {
        get {
            lock (_sync) {
                return _state;
            }
        }
    }

    public void MarkConnected() {
        TransitionTo(
            ReplicaConnectionState.Connected);
    }

    public void BeginHandshake() {
        TransitionTo(
            ReplicaConnectionState.Handshaking);
    }

    public void BeginSynchronization() {
        TransitionTo(
            ReplicaConnectionState.Synchronizing);
    }

    public void MarkOnline() {
        TransitionTo(
            ReplicaConnectionState.Online);
    }

    public void MarkDisconnected() {
        lock (_sync) {
            if (_disposed) {
                return;
            }

            _state =
                ReplicaConnectionState.Disconnected;
        }
    }

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        await _writeLock.WaitAsync(
            cancellationToken);

        try {
            ThrowIfDisposed();

            await _transport.WriteAsync(
                data,
                cancellationToken);
        }
        finally {
            _writeLock.Release();
        }
    }

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        return _transport.ReadAsync(
            buffer,
            cancellationToken);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;

        lock (_sync) {
            _state =
                ReplicaConnectionState.Disconnected;
        }

        await _transport.DisposeAsync();

        _writeLock.Dispose();
    }

    private void TransitionTo(
        ReplicaConnectionState newState) {
        lock (_sync) {
            ThrowIfDisposed();

            ValidateTransition(
                _state,
                newState);

            _state = newState;
        }
    }

    private static void ValidateTransition(
        ReplicaConnectionState current,
        ReplicaConnectionState next) {
        bool valid =
            (current, next) switch
            {
                (
                    ReplicaConnectionState.Disconnected,
                    ReplicaConnectionState.Connected
                ) => true,

                (
                    ReplicaConnectionState.Connected,
                    ReplicaConnectionState.Handshaking
                ) => true,

                (
                    ReplicaConnectionState.Handshaking,
                    ReplicaConnectionState.Synchronizing
                ) => true,

                (
                    ReplicaConnectionState.Synchronizing,
                    ReplicaConnectionState.Online
                ) => true,

                (
                    ReplicaConnectionState.Connected,
                    ReplicaConnectionState.Disconnected
                ) => true,

                (
                    ReplicaConnectionState.Handshaking,
                    ReplicaConnectionState.Disconnected
                ) => true,

                (
                    ReplicaConnectionState.Synchronizing,
                    ReplicaConnectionState.Disconnected
                ) => true,

                (
                    ReplicaConnectionState.Online,
                    ReplicaConnectionState.Disconnected
                ) => true,

                _ => false
            };

        if (!valid) {
            throw new InvalidOperationException(
                $"Invalid replica connection state transition: " +
                $"{current} -> {next}.");
        }
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}