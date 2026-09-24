using System.Net;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.Benchmarks.Infrastructure;

public class CacheVaultBenchmarkServer : IAsyncDisposable {
    private readonly ServiceProvider _serviceProvider;
    private readonly IRedisTcpServer _server;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _serverTask;
    private readonly string _dataDirectory;

    private CacheVaultBenchmarkServer(
        ServiceProvider serviceProvider,
        IRedisTcpServer server,
        CancellationTokenSource cancellation,
        Task serverTask,
        string dataDirectory) {
        _serviceProvider = serviceProvider;
        _server = server;
        _cancellation = cancellation;
        _serverTask = serverTask;
        _dataDirectory = dataDirectory;
    }

    public IPEndPoint Endpoint {
        get {
            if (_server.LocalEndpoint is null) {
                throw new InvalidOperationException(
                    "CacheVault benchmark server has not started.");
            }

            return _server.LocalEndpoint;
        }
    }

    public static async Task<CacheVaultBenchmarkServer> StartAsync(
        bool enableRdb = false,
        bool enableAof = false) {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "CacheVaultBenchmarks",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        var options = new ServerOptions
        {
            Host = "127.0.0.1",
            Port = 0,
            EnableRdb = enableRdb,
            EnableAof = enableAof,
            RdbFilePath = Path.Combine(
                directory,
                "cachevault.rdb"),
            AofFilePath = Path.Combine(
                directory,
                "cachevault.aof"),
            MaxMemoryBytes = 0
        };

        var services = new ServiceCollection();

        services.AddCacheVaultServer(options);

        ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IRedisTcpServer server =
            serviceProvider.GetRequiredService<IRedisTcpServer>();

        var cancellation =
            new CancellationTokenSource();

        Task serverTask =
            server.StartAsync(
                cancellation.Token);

        for (int attempt = 0; attempt < 100; attempt++) {
            if (server.LocalEndpoint is not null) {
                return new CacheVaultBenchmarkServer(
                    serviceProvider,
                    server,
                    cancellation,
                    serverTask,
                    directory);
            }

            if (serverTask.IsFaulted) {
                await serverTask.ConfigureAwait(false);
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(10));
        }

        await cancellation.CancelAsync();

        try {
            await serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
        }

        await serviceProvider
            .DisposeAsync()
            .ConfigureAwait(false);

        throw new TimeoutException(
            "CacheVault benchmark server did not start within the expected time.");
    }

    public async ValueTask DisposeAsync() {
        try {
            await _server.StopAsync(
                CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally {
            await _cancellation
                .CancelAsync()
                .ConfigureAwait(false);

            try {
                await _serverTask
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
            }

            _cancellation.Dispose();

            await _serviceProvider
                .DisposeAsync()
                .ConfigureAwait(false);

            try {
                Directory.Delete(
                    _dataDirectory,
                    recursive: true);
            }
            catch {
                // Benchmark cleanup must not hide the benchmark result.
            }
        }
    }
}