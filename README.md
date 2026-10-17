# CacheVault

Redis-compatible in-memory data store implemented in **C# / .NET** with TCP networking, RESP protocol support, transactions, optimistic concurrency, replication, persistence, eviction, Pub/Sub, lists, automated testing, and performance benchmarking.

---

## Features

### TCP Server

- TCP-based client/server communication
- Concurrent client connections
- Configurable host and port
- Graceful server startup and shutdown
- Connection lifecycle management
- Command dispatching
- Master and replica server modes
- CLI configuration

---

## RESP Protocol

RESP request parsing and response serialization for:

- Simple Strings
- Errors
- Integers
- Bulk Strings
- Arrays
- Multiple commands
- Pipelined commands

---

## Core Commands

Implemented commands:

```text
PING
ECHO
GET
SET
DEL
INCR
```

---

## Key Expiration / TTL

Implemented:

- Key expiration
- Expiration timestamps
- `SET` with millisecond expiration
- `PX`
- Expiration checks during key access
- TTL reset/update behavior
- Concurrent expiration handling

Example:

```text
SET name CacheVault PX 5000
```

---

## Transactions

Redis-style transaction support:

```text
MULTI
EXEC
DISCARD
```

Implemented:

- Transaction command queue
- `QUEUED` responses
- Atomic transaction execution
- Transaction isolation
- Transaction state management
- `EXEC` execution
- `DISCARD` transaction cancellation

Supported transactional commands include:

```text
GET
SET
DEL
INCR
```

---

## WATCH

Optimistic concurrency control with:

```text
WATCH
MULTI
EXEC
DISCARD
```

Implemented:

- Key version tracking
- Watched-key tracking
- Change detection
- Transaction invalidation
- Optimistic concurrency
- `WATCH` + `MULTI` + `EXEC` workflows

---

## Replication

Master/replica replication is implemented with:

- Master role
- Replica role
- Replication ID
- Replication offsets
- Replication state management
- Command propagation
- Replica acknowledgements
- `INFO replication`
- `REPLCONF`
- `PSYNC`
- `PSYNC2`
- `FULLRESYNC`
- Full synchronization
- Partial synchronization
- Replication backlog
- `WAIT`

Replication flow:

```text
                    ┌─────────────────┐
                    │      Master     │
                    │                 │
                    │  Command Store  │
                    └────────┬────────┘
                             │
                    Replication Stream
                             │
              ┌──────────────┴──────────────┐
              │                             │
              ▼                             ▼
      ┌───────────────┐             ┌───────────────┐
      │    Replica    │             │    Replica    │
      │       #1      │             │       #2      │
      └───────────────┘             └───────────────┘
```

---

## Replication Acknowledgement

Implemented replication acknowledgement mechanisms:

- Replica offset tracking
- Replica acknowledgement
- Waiting for replica synchronization
- `WAIT`

---

## Persistence

### RDB

Implemented snapshot persistence:

- Snapshot creation
- Snapshot loading
- Key/value persistence
- TTL persistence
- Startup restoration
- Configurable RDB directory
- Configurable RDB enable/disable

### AOF

Implemented append-only persistence:

- Command logging
- AOF replay
- Startup recovery
- Configurable AOF file
- Configurable AOF directory
- AOF enable/disable
- Command persistence

---

## Eviction

Configurable maximum-memory eviction with:

- LRU
- LFU
- Random eviction

Implemented:

- Maximum memory configuration
- Memory-limit enforcement
- Eviction policy selection
- Key removal under memory pressure
- Concurrent eviction handling

---

## Pub/Sub

Redis-style publish/subscribe functionality:

```text
SUBSCRIBE
PUBLISH
```

Implemented:

- Channel subscriptions
- Multiple channel subscriptions
- Message publishing
- Subscriber tracking
- Publisher-to-subscriber delivery
- Asynchronous message delivery
- Multiple connected subscribers

Example:

```text
SUBSCRIBE news
```

Another client:

```text
PUBLISH news hello
```

---

## Lists

Redis-style list data structure and commands:

```text
LPUSH
RPUSH
LPOP
RPOP
LRANGE
LLEN
```

Implemented:

- Left push
- Right push
- Left pop
- Right pop
- Range retrieval
- List length
- Multiple-element push
- Correct Redis-compatible list ordering
- Thread-safe list operations

Example:

```text
LPUSH numbers one two three
LRANGE numbers 0 -1
```

Result:

```text
three
two
one
```

---

## Concurrency

Implemented concurrency support across the server and data layer:

- Concurrent TCP clients
- Thread-safe key/value storage
- Concurrent transactions
- Concurrent list operations
- Concurrent TTL handling
- Concurrent Pub/Sub operations
- Concurrent replication handling
- Synchronization around shared server state

---

## Architecture

```text
                         ┌─────────────────────┐
                         │      TCP Client      │
                         │    redis-cli / App   │
                         └──────────┬──────────┘
                                    │
                                    │ RESP
                                    ▼
                         ┌─────────────────────┐
                         │   CacheVault Server │
                         └──────────┬──────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │    RESP Protocol    │
                         │ Parser / Serializer │
                         └──────────┬──────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │  Command Dispatcher │
                         └──────────┬──────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              │                     │                     │
              ▼                     ▼                     ▼
       ┌────────────┐        ┌────────────┐        ┌────────────┐
       │ Key/Value  │        │   Lists    │        │  Pub/Sub   │
       │   Store    │        │   Store    │        │   System   │
       └─────┬──────┘        └────────────┘        └────────────┘
             │
       ┌─────┼──────────────┬──────────────┐
       │     │              │              │
       ▼     ▼              ▼              ▼
      TTL  Eviction     Transactions   WATCH
       │                    │
       │                    │
       └──────────┬─────────┘
                  │
          ┌───────┴────────┐
          │                │
          ▼                ▼
    ┌───────────┐    ┌──────────────┐
    │    RDB    │    │     AOF      │
    │ Persistence│   │ Persistence  │
    └───────────┘    └──────────────┘

                  Replication
                       │
                       ▼
               ┌─────────────┐
               │    Master   │
               └──────┬──────┘
                      │
             ┌────────┴────────┐
             ▼                 ▼
        ┌──────────┐      ┌──────────┐
        │ Replica  │      │ Replica  │
        └──────────┘      └──────────┘
```

---

# Build

Restore dependencies:

```powershell
dotnet restore CacheVault.slnx
```

Build the complete solution:

```powershell
dotnet build CacheVault.slnx -c Release
```

---

# Testing

Run all tests:

```powershell
dotnet test CacheVault.slnx -c Release
```

Test projects:

```text
CacheVault.UnitTests
CacheVault.IntegrationTests
```

---

# Running CacheVault

Start CacheVault:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6379 --no-aof --no-rdb
```

Connect using `redis-cli`:

```powershell
redis-cli -p 6379
```

---

## Basic Commands

### PING

```text
PING
```

```text
PONG
```

### SET / GET

```text
SET name CacheVault
GET name
```

### INCR

```text
INCR counter
```

### DEL

```text
DEL name
```

### ECHO

```text
ECHO hello
```

---

# TTL Example

```text
SET session abc123 PX 10000
GET session
```

After the expiration period:

```text
GET session
```

returns a nil response.

---

# Transaction Example

```text
MULTI
SET name CacheVault
INCR counter
GET name
EXEC
```

---

# WATCH Example

```text
WATCH account
MULTI
SET account updated
EXEC
```

If a watched key is modified before `EXEC`, the transaction is invalidated.

---

# List Example

```text
LPUSH numbers one two three
LRANGE numbers 0 -1
```

Result:

```text
three
two
one
```

Additional operations:

```text
RPUSH numbers four
LLEN numbers
LPOP numbers
RPOP numbers
```

---

# Pub/Sub Example

Subscriber:

```text
SUBSCRIBE notifications
```

Publisher:

```text
PUBLISH notifications hello
```

The subscriber receives the published message asynchronously.

---

# Replication Example

Start the master:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6379
```

Start the replica:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6380 --replica --master-host 127.0.0.1 --master-port 6379
```

Check replication information:

```text
INFO replication
```

---

# Persistence

## RDB

Run with RDB enabled:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6379
```

Run with RDB disabled:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6379 --no-rdb
```

## AOF

Run with AOF enabled:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6379
```

Run with AOF disabled:

```powershell
dotnet run -c Release --project CacheVault.Server -- --host 127.0.0.1 --port 6379 --no-aof
```

---

## Benchmarks

CacheVault includes BenchmarkDotNet microbenchmarks and custom TCP load, list-scaling, and RDB snapshot/restore benchmarks. The measurements below are local results, not comparisons against Redis.

**Environment:** Windows 11 (build 10.0.26200), .NET SDK 10.0.301, .NET runtime 10.0.12, Intel Core Ultra 5 135H reported, 18 logical processors reported, Release build.

### Running the benchmarks

Build the benchmark project:

```powershell
dotnet build .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release
```

Run a 60-second mixed workload:

```powershell
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --load --clients 16 --duration 60 --workload mixed --sample-rate 100
```

Run a pipeline workload with batches of 100 commands:

```powershell
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --load --clients 16 --duration 60 --workload pipeline --pipeline 100 --sample-rate 100
```

Run list scaling at 1K, 10K, and 100K items:

```powershell
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --list-scaling --sizes "1000,10000,100000" --repeats 5 --samples 500 --warmup 20
```

Run five RDB trials with 50K string keys:

```powershell
dotnet run --project .\benchmarks\CacheVault.Benchmarks\CacheVault.Benchmarks.csproj -c Release --no-build -- --rdb-benchmark --keys 50000 --repeats 5
```

### Recent local results

These results come from separate run sets and configurations. Percentiles are summaries of the recorded runs; custom TCP load-test latency is sampled at 1 in every 100 workload iterations.

| Workload | Configuration | Observed result | Latency / interpretation |
|---|---|---|---|
| Mixed GET/SET/INCR | 16 clients, 2 runs | 77,022 ops/s mean | Mean run p50/p95/p99: 0.173 / 0.271 / 0.463 ms; zero errors |
| Pipeline | 16 clients, pipeline depth 100, 2 runs | 238,992 commands/s mean | p50/p95/p99: 5.477 / 12.340 / 17.878 ms **per batch of 100 commands**; worst sampled batch latency 330.159 ms; zero errors |
| SET, AOF OFF | 16 clients, 2 runs | 86,624 ops/s mean | Mean run p50/p95/p99: 0.152 / 0.253 / 0.551 ms |
| SET, AOF ON | 16 clients, 2 runs | 26,098 ops/s mean | Mean run p50/p95/p99: 0.563 / 0.846 / 1.870 ms; approximately 69.9% lower throughput than AOF OFF in this preliminary comparison |

### Command-gate diagnostic

A five-trial GET-only study compared the command gate enabled with a diagnostic-only bypass. The bypass must not be treated as a production mode or evidence that the synchronization boundary can safely be removed.

| Clients | Condition | Median throughput | Median p50 / p95 / p99 (ms) |
|---:|---|---:|---:|
| 16 | Gate enabled | 50,797 ops/s | 0.254 / 0.453 / 1.850 |
| 16 | Diagnostic bypass | 73,770 ops/s | 0.177 / 0.336 / 1.266 |
| 64 | Gate enabled | 48,113 ops/s | 1.073 / 2.463 / 5.861 |
| 64 | Diagnostic bypass | 47,511 ops/s | 1.060 / 2.862 / 6.851 |

At 64 clients, bypassing the gate did not improve median throughput; gate-on p95/p99 were lower in all five same-trial comparisons. At 16 clients, variation was substantial and the observed median difference does not establish causality.

An earlier two-run 64-client GET-only set measured approximately 95,028 ops/s. Its exact source revision and full raw output were not retained, and the discrepancy versus the later five-trial study remains unresolved. Treat that earlier result as historical rather than a directly comparable baseline.

### List-scaling result

For 100K-item lists, a selected comparison of three baseline captures against three optimized captures measured these reductions in the median of the per-run latency percentiles, after incremental list-value memory accounting. The benchmark used one sequential TCP client, 16-byte UTF-8 values, `LRANGE 0–99`, and no eviction pressure (`MaxMemoryBytes=0`). The detailed baseline and optimized latency values are in [CacheVault_Benchmark_Report.md](CacheVault_Benchmark_Report.md).

| Operation | p50 reduction | p95 reduction | p99 reduction |
|---|---:|---:|---:|
| LPUSH | 89.1% | 90.0% | 87.8% |
| RPUSH | 90.9% | 91.0% | 88.8% |
| LPOP | 87.5% | 86.8% | 85.7% |
| RPOP | 91.2% | 92.0% | 91.1% |
| LRANGE 0–99 | 11.5% | 27.9% | 18.2% |

These are local results from the selected captures, not guarantees for other list sizes or workloads. The accounting change removes a full-list UTF-8 byte-count recalculation but does not remove the O(n) element movement from `List<string>` head insertion/removal. The `LRANGE` change is an observation from the same comparison, not evidence that the accounting change directly optimized range retrieval. The experiment did not measure behavior under active eviction or memory pressure. Individual CSVs do not embed their own source commit IDs, so source revision provenance is incomplete; the full report documents this limitation.

### RDB snapshot and restore

Five trials were run per dataset size. Snapshot timing includes graceful shutdown; restore timing includes startup and TCP endpoint readiness. Restore verification samples selected keys and the first list, not every restored key.

| String keys | RDB file size | Median shutdown + snapshot | Median restore to ready |
|---:|---:|---:|---:|
| 1,000 | 78,988 B | 2.02 ms | 1.53 ms |
| 10,000 | 799,735 B | 5.68 ms | 20.00 ms |
| 50,000 | 4,043,095 B | 30.63 ms | 72.19 ms |

The managed-heap delta is excluded because it is too noisy to represent total dataset memory. Snapshot and restore timings have noticeable run-to-run variation.

For the full methodology, individual trials, limitations, and raw-evidence inventory, see [CacheVault_Benchmark_Report.md](CacheVault_Benchmark_Report.md).
