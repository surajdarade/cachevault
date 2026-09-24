using CacheVault.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace CacheVault.Benchmarks.Benchmarks;

public abstract class TcpBenchmarkBase {
    protected CacheVaultBenchmarkServer Server = null!;
    protected BenchmarkRedisClient Client = null!;

    [GlobalSetup]
    public async Task GlobalSetupAsync() {
        Server =
            await CacheVaultBenchmarkServer.StartAsync()
                .ConfigureAwait(false);

        Client =
            await BenchmarkRedisClient.ConnectAsync(
                Server.Endpoint)
                .ConfigureAwait(false);

        await Client.SendAsync(
            BenchmarkRedisClient.Command(
                "SET",
                "benchmark:get",
                "value"))
            .ConfigureAwait(false);

        await PrepareBenchmarkAsync()
            .ConfigureAwait(false);
    }

    protected virtual Task PrepareBenchmarkAsync() {
        return Task.CompletedTask;
    }

    [GlobalCleanup]
    public async Task GlobalCleanupAsync() {
        if (Client is not null) {
            await Client.DisposeAsync()
                .ConfigureAwait(false);
        }

        if (Server is not null) {
            await Server.DisposeAsync()
                .ConfigureAwait(false);
        }
    }
}