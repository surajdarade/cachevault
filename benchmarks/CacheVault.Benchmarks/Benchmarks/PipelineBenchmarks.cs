using BenchmarkDotNet.Attributes;
using CacheVault.Benchmarks.Infrastructure;

namespace CacheVault.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class PipelineBenchmarks : TcpBenchmarkBase {
    [Params(1, 10, 100, 500)]
    public int PipelineDepth { get; set; }

    private byte[] _pipeline = null!;

    protected override Task PrepareBenchmarkAsync() {
        var commands =
            new byte[PipelineDepth][];

        for (int index = 0;
             index < PipelineDepth;
             index++) {
            commands[index] =
                BenchmarkRedisClient.Command(
                    "GET",
                    "benchmark:get");
        }

        _pipeline =
            BenchmarkRedisClient.Pipeline(
                commands);

        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task GetPipeline() {
        await Client.SendPipelineAsync(
            _pipeline,
            PipelineDepth)
            .ConfigureAwait(false);
    }
}