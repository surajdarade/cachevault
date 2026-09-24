using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[SimpleJob(
    launchCount: 1,
    warmupCount: 3,
    iterationCount: 5)]
[MemoryDiagnoser]
public class ListBenchmarks : TcpBenchmarkBase {
    private byte[] _lpush = null!;
    private byte[] _rpush = null!;
    private byte[] _llen = null!;
    private byte[] _lrange = null!;
    private byte[] _lpop = null!;
    private byte[] _rpop = null!;

    protected override async Task PrepareBenchmarkAsync() {
        _lpush =
            BenchmarkRedisClient.Command(
                "LPUSH",
                "benchmark:list:push",
                "value");

        _rpush =
            BenchmarkRedisClient.Command(
                "RPUSH",
                "benchmark:list:push",
                "value");

        _llen =
            BenchmarkRedisClient.Command(
                "LLEN",
                "benchmark:list");

        _lrange =
            BenchmarkRedisClient.Command(
                "LRANGE",
                "benchmark:list",
                "0",
                "99");

        _lpop =
            BenchmarkRedisClient.Command(
                "LPOP",
                "benchmark:list:pop");

        _rpop =
            BenchmarkRedisClient.Command(
                "RPOP",
                "benchmark:list:pop");

        await Client.SendAsync(
            BenchmarkRedisClient.Command(
                "RPUSH",
                "benchmark:list",
                "one",
                "two",
                "three",
                "four",
                "five"))
            .ConfigureAwait(false);

        await Client.SendAsync(
            BenchmarkRedisClient.Command(
                "RPUSH",
                "benchmark:list:pop",
                "one",
                "two",
                "three",
                "four",
                "five"))
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Lpush() {
        await Client.SendAsync(_lpush)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Rpush() {
        await Client.SendAsync(_rpush)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Llen() {
        await Client.SendAsync(_llen)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Lrange() {
        await Client.SendAsync(_lrange)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task LpopRoundTrip() {
        await Client.SendAsync(
            _lpush)
            .ConfigureAwait(false);

        await Client.SendAsync(
            _lpop)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task RpopRoundTrip() {
        await Client.SendAsync(
            _rpush)
            .ConfigureAwait(false);

        await Client.SendAsync(
            _rpop)
            .ConfigureAwait(false);
    }
}