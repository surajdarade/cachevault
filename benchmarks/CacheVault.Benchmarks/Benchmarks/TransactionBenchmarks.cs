using BenchmarkDotNet.Attributes;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class TransactionBenchmarks : TcpBenchmarkBase {
    private byte[] _transaction = null!;

    protected override Task PrepareBenchmarkAsync() {
        var commands =
            new List<byte[]>
            {
                BenchmarkRedisClient.Command("MULTI"),

                BenchmarkRedisClient.Command(
                    "SET",
                    "benchmark:tx:1",
                    "one"),

                BenchmarkRedisClient.Command(
                    "SET",
                    "benchmark:tx:2",
                    "two"),

                BenchmarkRedisClient.Command(
                    "INCR",
                    "benchmark:tx:counter"),

                BenchmarkRedisClient.Command("EXEC")
            };

        _transaction =
            BenchmarkRedisClient.Pipeline(commands);

        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task MultiExec() {
        await Client.SendPipelineAsync(
            _transaction,
            5)
            .ConfigureAwait(false);
    }
}