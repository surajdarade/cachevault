using BenchmarkDotNet.Attributes;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MixedWorkloadBenchmarks : TcpBenchmarkBase {
    private readonly Random _random = new(42);

    private byte[][] _requests = null!;

    protected override Task PrepareBenchmarkAsync() {
        _requests =
        [
            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:get"),

            BenchmarkRedisClient.Command(
                "SET",
                "benchmark:mixed",
                "value"),

            BenchmarkRedisClient.Command(
                "SET",
                "benchmark:mixed",
                "value"),

            BenchmarkRedisClient.Command(
                "INCR",
                "benchmark:mixed:counter")
        ];

        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task SeventyPercentGetTwentyPercentSetTenPercentIncr() {
        int index =
            _random.Next(
                _requests.Length);

        await Client.SendAsync(
            _requests[index])
            .ConfigureAwait(false);
    }
}