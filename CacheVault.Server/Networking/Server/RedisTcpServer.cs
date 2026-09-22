using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Server;

public sealed class RedisTcpServer : IRedisTcpServer {
    private readonly ServerOptions _options;

    private readonly IClientConnectionHandler _connectionHandler;

    private readonly ConcurrentDictionary<Guid, Task> _clientTasks =
        new();

    private TcpListener? _listener;

    public RedisTcpServer(
        ServerOptions options,
        IClientConnectionHandler connectionHandler) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionHandler);

        _options = options;
        _connectionHandler = connectionHandler;
    }

    public IPEndPoint? LocalEndpoint =>
        _listener?.LocalEndpoint as IPEndPoint;

    public async Task StartAsync(
        CancellationToken cancellationToken) {
        if (_listener is not null) {
            throw new InvalidOperationException(
                "The TCP server has already been started.");
        }

        IPAddress address =
            IPAddress.Parse(_options.Host);

        _listener =
            new TcpListener(
                address,
                _options.Port);

        _listener.Start();

        try {
            while (!cancellationToken.IsCancellationRequested) {
                TcpClient client =
                    await _listener.AcceptTcpClientAsync(
                        cancellationToken);

                Guid connectionId =
                    Guid.NewGuid();

                Task clientTask =
                    HandleClientAsync(
                        connectionId,
                        client,
                        cancellationToken);

                _clientTasks.TryAdd(
                    connectionId,
                    clientTask);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
            // Normal shutdown.
        }
        finally {
            _listener.Stop();
            _listener = null;
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken) {
        _listener?.Stop();

        Task[] clientTasks =
            _clientTasks.Values.ToArray();

        if (clientTasks.Length == 0) {
            return;
        }

        await Task.WhenAll(
            clientTasks.Select(
                task => task.WaitAsync(
                    cancellationToken)));
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