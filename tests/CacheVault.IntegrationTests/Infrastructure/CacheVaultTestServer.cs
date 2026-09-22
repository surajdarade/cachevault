using System.Net;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.IntegrationTests.Infrastructure;

public sealed class CacheVaultTestServer : IAsyncDisposable {
    private readonly ServiceProvider _serviceProvider;

    private readonly IRedisTcpServer _server;

    private readonly CancellationTokenSource _cancellationTokenSource =
        new();

    private Task? _serverTask;

    public CacheVaultTestServer() {
        var services =
            new ServiceCollection();

        var options =
            new ServerOptions
            {
                Host = "127.0.0.1",
                Port = 0
            };

        services.AddCacheVaultServer(
            options);

        _serviceProvider =
            services.BuildServiceProvider();

        _server =
            _serviceProvider.GetRequiredService<IRedisTcpServer>();
    }

    public IPEndPoint LocalEndpoint =>
        _server.LocalEndpoint
        ?? throw new InvalidOperationException(
            "CacheVault server is not listening.");

    public async Task StartAsync() {
        _serverTask =
            _server.StartAsync(
                _cancellationTokenSource.Token);

        for (int attempt = 0; attempt < 100; attempt++) {
            if (_server.LocalEndpoint is not null) {
                return;
            }

            await Task.Delay(10);
        }

        throw new InvalidOperationException(
            "CacheVault server failed to start.");
    }

    public async ValueTask DisposeAsync() {
        _cancellationTokenSource.Cancel();

        if (_serverTask is not null) {
            try {
                await _serverTask;
            }
            catch (OperationCanceledException) {
                // Expected during shutdown.
            }
        }

        _cancellationTokenSource.Dispose();

        await _serviceProvider.DisposeAsync();
    }
}