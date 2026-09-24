using BenchmarkDotNet.Attributes;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class PayloadSizeBenchmarks : TcpBenchmarkBase {
    [Params(64, 1024, 16 * 1024, 64 * 1024)]
    public int PayloadBytes { get; set; }

    private byte[] _set = null!;
    private byte[] _get = null!;

    protected override async Task PrepareBenchmarkAsync() {
        string value =
            new(
                'x',
                PayloadBytes);

        _set =
            BenchmarkRedisClient.Command(
                "SET",
                "benchmark:payload",
                value);

        _get =
            BenchmarkRedisClient.Command(
                "GET",
                "benchmark:payload");

        await Client.SendAsync(
            _set)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task SetPayload() {
        await Client.SendAsync(
            _set)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task GetPayload() {
        await Client.SendAsync(
            _get)
            .ConfigureAwait(false);
    }
}