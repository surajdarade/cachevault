namespace CacheVault.Benchmarks;

public static class BenchmarkHelp
{
    public static void Print()
    {
        Console.WriteLine("""
CacheVault Benchmarking
=======================

BenchmarkDotNet:
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *CommandThroughput*
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *Pipeline*
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *Payload*
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *List*
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *Transaction*
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *Mixed*

List scaling latency benchmark (one TCP client; AOF/RDB off):
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --list-scaling

List scaling options:
  --sizes <comma-separated-counts>  List sizes. Default: 1000,10000,100000
  --repeats <count>                 Independent server trials per size. Default: 5
  --samples <count>                 Measured samples per operation per trial. Default: 100
  --warmup <count>                  Warm-up operations per operation per trial. Default: 20

Real TCP load tests:
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load

Load options:
  --duration <seconds>       Test duration. Default: 30
  --clients <count>          Concurrent TCP clients. Default: 16
  --workload <name>          get|set|mixed|pipeline|transaction|list
  --pipeline <depth>         Commands per pipeline. Default: 100
  --sample-rate <N>          Record 1 latency sample per N operations. Default: 100
  --aof                       Enable AOF for the load test
  --rdb                       Enable RDB for the load test
  --bypass-command-gate       Diagnostic GET-only run; bypass dispatch gate

Examples:
  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 64 --duration 60 --workload mixed

  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 16 --duration 30 --workload get --bypass-command-gate

  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 64 --duration 60 --workload pipeline --pipeline 100

  dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 32 --duration 60 --workload transaction

The load runner reports:
  throughput (operations/sec)
  total operations
  errors
  sampled p50/p95/p99/max latency
""");
    }
}
