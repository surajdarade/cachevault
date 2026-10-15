```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
Intel Core Ultra 5 135H 1.70GHz, 1 CPU, 18 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-IJPESX : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=5  LaunchCount=1  WarmupCount=3  

```
| Method        | Mean     | Error     | StdDev   | Gen0   | Allocated |
|-------------- |---------:|----------:|---------:|-------:|----------:|
| Lpush         | 389.5 μs |  78.66 μs | 12.17 μs | 0.2441 |   2.93 KB |
| Rpush         | 251.3 μs | 187.22 μs | 48.62 μs | 0.2441 |   2.93 KB |
| Llen          | 139.2 μs |   8.96 μs |  2.33 μs | 0.1221 |   1.61 KB |
| Lrange        | 144.9 μs |  10.47 μs |  2.72 μs | 0.2441 |   3.05 KB |
| LpopRoundTrip | 387.3 μs | 152.85 μs | 23.65 μs | 0.4883 |   5.32 KB |
| RpopRoundTrip | 201.7 μs |  78.75 μs | 20.45 μs | 0.4883 |   5.29 KB |
