using BenchmarkDotNet.Attributes;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class CommandThroughputBenchmarks : TcpBenchmarkBase {
    private readonly byte[] _ping =
        BenchmarkRedisClient.Command(
            "PING");

    private readonly byte[] _get =
        BenchmarkRedisClient.Command(
            "GET",
            "benchmark:get");

    private readonly byte[] _set =
        BenchmarkRedisClient.Command(
            "SET",
            "benchmark:set",
            "value");

    private readonly byte[] _incr =
        BenchmarkRedisClient.Command(
            "INCR",
            "benchmark:counter");

    [Benchmark(Baseline = true)]
    public async Task Ping() {
        await Client.SendAsync(
            _ping)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Get() {
        await Client.SendAsync(
            _get)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Set() {
        await Client.SendAsync(
            _set)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Incr() {
        await Client.SendAsync(
            _incr)
            .ConfigureAwait(false);
    }
}