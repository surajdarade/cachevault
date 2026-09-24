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

# Benchmark Results

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

# Concurrent Load Results

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