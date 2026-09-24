using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[SimpleJob(
    launchCount: 1,
    warmupCount: 3,
    iterationCount: 5)]
[MemoryDiagnoser]
public class PubSubBenchmarks {
    private CacheVaultBenchmarkServer _server = null!;
    private BenchmarkRedisClient _subscriber = null!;
    private BenchmarkRedisClient _publisher = null!;
    private byte[] _publish = null!;

    [GlobalSetup]
    public async Task Setup() {
        _server =
            await CacheVaultBenchmarkServer.StartAsync()
                .ConfigureAwait(false);

        _subscriber =
            await BenchmarkRedisClient.ConnectAsync(
                _server.Endpoint)
                .ConfigureAwait(false);

        _publisher =
            await BenchmarkRedisClient.ConnectAsync(
                _server.Endpoint)
                .ConfigureAwait(false);

        await _subscriber.SendAsync(
            BenchmarkRedisClient.Command(
                "SUBSCRIBE",
                "benchmark:news"))
            .ConfigureAwait(false);

        _publish =
            BenchmarkRedisClient.Command(
                "PUBLISH",
                "benchmark:news",
                "hello");
    }

    [Benchmark]
    public async Task PublishAndDeliver() {
        await _publisher.SendAsync(
            _publish)
            .ConfigureAwait(false);

        await _subscriber.ReadResponseAsync()
            .ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup() {
        if (_subscriber is not null) {
            await _subscriber.DisposeAsync()
                .ConfigureAwait(false);
        }

        if (_publisher is not null) {
            await _publisher.DisposeAsync()
                .ConfigureAwait(false);
        }

        if (_server is not null) {
            await _server.DisposeAsync()
                .ConfigureAwait(false);
        }
    }
}