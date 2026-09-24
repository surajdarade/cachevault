using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using CacheVault.Server.Recovery;

namespace CacheVault.Server.Networking.Server;

public sealed class RedisTcpServer :
    IRedisTcpServer {
    private readonly ServerOptions _options;

    private readonly IConnectionHandler _connectionHandler;

    private readonly RecoveryCoordinator _recoveryCoordinator;

    private readonly ConcurrentDictionary<Guid, Task> _clientTasks =
        new();

    private readonly object _lifecycleLock =
        new();

    private TcpListener? _listener;

    private CancellationTokenSource? _serverCancellationTokenSource;

    public RedisTcpServer(
        ServerOptions options,
        IConnectionHandler connectionHandler,
        RecoveryCoordinator recoveryCoordinator) {
        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            connectionHandler);

        ArgumentNullException.ThrowIfNull(
            recoveryCoordinator);

        _options =
            options;

        _connectionHandler =
            connectionHandler;

        _recoveryCoordinator =
            recoveryCoordinator;
    }

    public IPEndPoint? LocalEndpoint =>
        _listener?.LocalEndpoint as IPEndPoint;

    public async Task StartAsync(
        CancellationToken cancellationToken) {
        lock (_lifecycleLock) {
            if (_listener is not null) {
                throw new InvalidOperationException(
                    "The TCP server has already been started.");
            }

            _serverCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
        }

        CancellationToken serverCancellationToken =
            _serverCancellationTokenSource.Token;

        try {
            await _recoveryCoordinator
                .RecoverAsync(
                    _options,
                    DateTimeOffset.UtcNow,
                    serverCancellationToken)
                .ConfigureAwait(false);

            lock (_lifecycleLock) {
                if (serverCancellationToken.IsCancellationRequested) {
                    return;
                }

                IPAddress address =
                    IPAddress.Parse(
                        _options.Host);

                _listener =
                    new TcpListener(
                        address,
                        _options.Port);

                _listener.Start();
            }

            while (!serverCancellationToken.IsCancellationRequested) {
                TcpClient client;

                try {
                    client =
                        await _listener.AcceptTcpClientAsync(
                            serverCancellationToken);
                }
                catch (OperationCanceledException)
                    when (serverCancellationToken.IsCancellationRequested) {
                    break;
                }
                catch (SocketException)
                    when (serverCancellationToken.IsCancellationRequested) {
                    break;
                }

                Guid connectionId =
                    Guid.NewGuid();

                Task clientTask =
                    HandleClientAsync(
                        connectionId,
                        client,
                        serverCancellationToken);

                _clientTasks.TryAdd(
                    connectionId,
                    clientTask);
            }
        }
        finally {
            StopListener();

            CancellationTokenSource?
                serverCancellationTokenSource;

            lock (_lifecycleLock) {
                serverCancellationTokenSource =
                    _serverCancellationTokenSource;

                _serverCancellationTokenSource =
                    null;
            }

            serverCancellationTokenSource?.Dispose();
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken) {
        CancellationTokenSource?
            serverCancellationTokenSource;

        lock (_lifecycleLock) {
            serverCancellationTokenSource =
                _serverCancellationTokenSource;
        }

        if (serverCancellationTokenSource is null) {
            return;
        }

        serverCancellationTokenSource.Cancel();

        StopListener();

        Task[] clientTasks =
            _clientTasks.Values.ToArray();

        if (clientTasks.Length == 0) {
            return;
        }

        await Task.WhenAll(
            clientTasks.Select(
                task =>
                    task.WaitAsync(
                        cancellationToken)));
    }

    private void StopListener() {
        TcpListener? listener;

        lock (_lifecycleLock) {
            listener =
                _listener;

            _listener =
                null;
        }

        listener?.Stop();
    }

    private async Task HandleClientAsync(
        Guid connectionId,
        TcpClient client,
        CancellationToken cancellationToken) {
        try {
            await _connectionHandler.HandleAsync(
                client,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
        }
        finally {
            _clientTasks.TryRemove(
                connectionId,
                out _);

            client.Dispose();
        }
    }
}