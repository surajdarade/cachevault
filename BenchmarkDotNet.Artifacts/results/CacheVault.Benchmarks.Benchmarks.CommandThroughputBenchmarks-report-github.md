```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
Intel Core Ultra 5 135H 1.70GHz, 1 CPU, 18 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------- |---------:|---------:|---------:|-------:|----------:|
| Incr   | 80.65 μs | 2.354 μs | 6.523 μs | 0.2441 |   2.75 KB |
