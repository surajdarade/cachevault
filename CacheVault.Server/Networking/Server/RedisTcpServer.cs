using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using CacheVault.Server.Recovery;
using CacheVault.Server.Replication;

namespace CacheVault.Server.Networking.Server;

public sealed class RedisTcpServer :
    IRedisTcpServer {
    private readonly ServerOptions _options;

    private readonly IConnectionHandler _connectionHandler;

    private readonly RecoveryCoordinator _recoveryCoordinator;
    private readonly RdbPersistenceService _rdbPersistenceService;
    private readonly ReplicaSynchronizationService? _replicaSynchronizationService;

    private readonly ConcurrentDictionary<Guid, Task> _clientTasks =
        new();

    private readonly object _lifecycleLock =
        new();

    private TcpListener? _listener;

    private CancellationTokenSource? _serverCancellationTokenSource;
    private Task? _replicaSynchronizationTask;
    private bool _serverStarted;

    public RedisTcpServer(
        ServerOptions options,
        IConnectionHandler connectionHandler,
        RecoveryCoordinator recoveryCoordinator,
        RdbPersistenceService rdbPersistenceService,
        ReplicaSynchronizationService? replicaSynchronizationService = null) {
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

        ArgumentNullException.ThrowIfNull(
            rdbPersistenceService);

        _rdbPersistenceService =
            rdbPersistenceService;

        _replicaSynchronizationService =
            replicaSynchronizationService;
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
                _serverStarted = true;

                if (_options.IsReplica &&
                    _replicaSynchronizationService is not null)
                {
                    _replicaSynchronizationTask =
                        _replicaSynchronizationService.RunAsync(
                            serverCancellationToken);
                }
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
        finally
        {
            StopListener();

            try
            {
                _serverCancellationTokenSource?.Cancel();

                if (_replicaSynchronizationTask is not null)
                {
                    await _replicaSynchronizationTask;
                }

                Task[] clientTasks =
                    _clientTasks.Values.ToArray();

                if (clientTasks.Length > 0)
                {
                    await Task.WhenAll(
                        clientTasks);
                }

                if (_serverStarted)
                {
                    await _rdbPersistenceService.SaveAsync(
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _replicaSynchronizationTask = null;
            }

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
        CancellationToken cancellationToken)
    {
        CancellationTokenSource?
            serverCancellationTokenSource;

        lock (_lifecycleLock)
        {
            serverCancellationTokenSource =
                _serverCancellationTokenSource;
        }

        if (serverCancellationTokenSource is null)
        {
            return;
        }

        serverCancellationTokenSource.Cancel();

        StopListener();

        if (_replicaSynchronizationTask is not null)
        {
            await _replicaSynchronizationTask.WaitAsync(
                cancellationToken);

            _replicaSynchronizationTask = null;
        }

        Task[] clientTasks =
            _clientTasks.Values.ToArray();

        if (clientTasks.Length > 0)
        {
            await Task.WhenAll(
                clientTasks.Select(
                    task =>
                        task.WaitAsync(
                            cancellationToken)));
        }

        await _rdbPersistenceService.SaveAsync(
            cancellationToken);
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