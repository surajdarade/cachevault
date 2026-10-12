# CacheVault Benchmarking

## Run Benchmarks

### Build

```powershell
dotnet build benchmarks/CacheVault.Benchmarks/CacheVault.Benchmarks.csproj -c Release
```

### Run All BenchmarkDotNet Benchmarks

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks
```

### Run Specific Benchmarks

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *CommandThroughputBenchmarks*
```

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *PipelineBenchmarks*
```

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *PayloadSizeBenchmarks*
```

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *TransactionBenchmarks*
```

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *MixedWorkloadBenchmarks*
```

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --filter *PubSubBenchmarks*
```

---

## Concurrent Load Testing

### Default

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load
```

### 64 Clients

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 64 --duration 60 --workload mixed
```

### 128 Clients

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 128 --duration 60 --workload mixed
```

### GET

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 64 --duration 60 --workload get
```

### SET

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 64 --duration 60 --workload set
```

### Pipeline

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 64 --duration 60 --workload pipeline --pipeline 100
```

### Transactions

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 32 --duration 60 --workload transaction
```

### Lists

```powershell
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks -- --load --clients 32 --duration 60 --workload list
```

---

## RDB Snapshot and Restore Benchmark

Build the benchmark project first, then run a small smoke test:

```powershell
dotnet build .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --rdb-benchmark --keys 1000 --repeats 1
```

Repeat a dataset size for five trials:

```powershell
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --rdb-benchmark --keys 10000 --repeats 5
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --rdb-benchmark --keys 50000 --repeats 5
```

Runner semantics and limitations:

- AOF is disabled; RDB is enabled; each string value is 64 UTF-8 bytes. The runner creates `max(1, keys / 1000)` lists with 10 items each.
- “Shutdown + snapshot” is graceful shutdown through server-task completion, not isolated serialization time.
- “Restore to ready” is startup through TCP endpoint readiness, not isolated deserialization time.
- Restore validation samples the first, middle, and last string keys and checks the first list; it does not verify every restored key.
- The managed-heap delta is an approximate in-process before/after measurement. It is affected by GC and runtime allocations and must not be used as total dataset memory, peak RSS, or per-key memory overhead.
- Keep every trial, including slow runs. Report the median and individual measurements; do not silently discard outliers.

## Recent Local Measurements

Environment recorded for these runs: Windows 10.0.26200, .NET SDK 10.0.301, .NET runtime 10.0.12, Intel Core Ultra 5 135H reported, 18 logical processors reported, Release build. AOF and RDB were disabled for the RESP load tests unless stated otherwise. These are local results, not a Redis comparison.

### RESP load tests

All load tests below used a 60-second measurement window. Percentile summaries in this table are arithmetic averages of per-run percentiles, not pooled request-level percentiles.

| Workload | Clients / pipeline | Mean throughput | Mean p50 | Mean p95 | Mean p99 | Errors |
|---|---:|---:|---:|---:|---:|---:|
| GET-only | 64 clients, 2 runs | 95,028 ops/s | 0.573 ms | 0.876 ms | 2.784 ms | 0 in both runs |
| Mixed (70% GET, 20% SET, 10% INCR) | 16 clients, 2 runs | 77,022 ops/s | 0.173 ms | 0.271 ms | 0.463 ms | 0 in both runs |
| Pipeline | 16 clients, depth 100, 2 runs | 238,992 commands/s | 5.477 ms | 12.340 ms | 17.878 ms | 0 in both runs |
| SET, persistence OFF | 16 clients, 2 runs | 86,624 ops/s | 0.152 ms | 0.253 ms | 0.551 ms | 0 in both runs |
| SET, AOF ON | 16 clients, 2 runs | 26,098 ops/s | 0.563 ms | 0.846 ms | 1.870 ms | 0 in both runs |

**Pipeline latency is per batch of 100 commands**, not per individual command. The two pipeline runs had maximum batch latencies of 330.159 ms and 312.224 ms. The observed SET throughput with AOF enabled was about 69.9% lower than the persistence-disabled baseline in this preliminary two-run comparison. Treat this as a local observation, not a universal AOF penalty. The AOF writer calls `FlushAsync()` per append; that alone does not establish physical-media `fsync` durability.

GET-only 16-client runs varied: one run measured 104,470 ops/s, while two later runs measured 86,461 and 86,562 ops/s. Do not claim a definitive optimal client count from these runs.

### RDB snapshot and restore

Five trials were run at each dataset size. Snapshot time includes graceful shutdown overhead; restore time includes startup and listener readiness. File size was identical across the five trials for each size.

| String keys | Median seed time | Median shutdown + snapshot | RDB file size | Median restore to ready |
|---:|---:|---:|---:|---:|
| 1,000 | 27.91 ms | 2.02 ms | 78,988 B | 1.53 ms |
| 10,000 | 261.47 ms | 5.68 ms | 799,735 B | 20.00 ms |
| 50,000 | 1,943.60 ms | 30.63 ms | 4,043,095 B | 72.19 ms |

These are medians of five trials on one machine. The individual runs show meaningful timing variability; retain raw trials when citing these figures. The managed-heap delta is excluded from headline results because it is noisy and does not estimate total dataset memory. The RDB runner verifies sampled keys and the first list rather than every restored key.

---

# Historical BenchmarkDotNet Results

The following BenchmarkDotNet and concurrent-load tables are preserved from an earlier README snapshot. Their raw output, exact source revision, and full measurement metadata were not provided with the current run record. Treat them as historical and **not directly comparable** with the recent local measurements above.

## Command Throughput

| Command | Mean | Allocated |
|---|---:|---:|
| PING | 150.46 μs | 1.47 KB |
| GET | 141.10 μs | 1.67 KB |
| SET | 98.71 μs | 2.94 KB |
| INCR | 130.93 μs | 2.75 KB |

## Pipeline

| Depth | Mean | Allocated |
|---:|---:|---:|
| 1 | 89.66 μs | 1.68 KB |
| 10 | 294.98 μs | 10.58 KB |
| 100 | 1,740.50 μs | 99.73 KB |
| 500 | 7,272.34 μs | 495.87 KB |

## Payload Size

| Operation | Payload | Mean | Allocated |
|---|---:|---:|---:|
| SET | 64 B | 65.51 μs | 3.21 KB |
| GET | 64 B | 70.83 μs | 1.91 KB |
| SET | 1 KB | 72.53 μs | 10.46 KB |
| GET | 1 KB | 71.74 μs | 8.74 KB |
| SET | 16 KB | 106.41 μs | 116.16 KB |
| GET | 16 KB | 90.55 μs | 113.74 KB |
| SET | 64 KB | 200.85 μs | 452.16 KB |
| GET | 64 KB | 195.17 μs | 449.74 KB |

## Transactions

| Benchmark | Mean | Allocated |
|---|---:|---:|
| MULTI / EXEC | 210.0 μs | 9.72 KB |

## Mixed Workload

```text
GET   : 70%
SET   : 20%
INCR  : 10%
```

| Benchmark | Mean | Allocated |
|---|---:|---:|
| Mixed Workload | 187.6 μs | 2.04 KB |

## Pub/Sub

| Benchmark | Mean | Allocated |
|---|---:|---:|
| Publish + Deliver | 92.53 μs | 3.24 KB |

---

# Historical Concurrent Load Results

| Clients | Duration | Operations | Throughput | Errors | p50 | p95 | p99 |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 16 | 30s | 1,747,713 | 58,239 ops/s | 0 | 0.226 ms | 0.414 ms | 0.976 ms |
| 64 | 60s | 3,682,567 | 61,353 ops/s | 0 | 0.842 ms | 1.592 ms | 5.596 ms |
| 128 | 60s | 4,008,740 | 66,787 ops/s | 0 | 1.603 ms | 3.679 ms | 7.976 ms |

## Test Environment

```text
CPU     : Intel Core Ultra 5 135H
Cores   : 14 Physical / 18 Logical
Runtime : .NET 10.0.12
OS      : Windows 11 25H2
AOF     : OFF
RDB     : OFF
```