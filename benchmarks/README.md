# CacheVault

**CacheVault** is a Redis-compatible distributed persistent in-memory data store implemented completely from scratch in **C# / .NET 8**.

It uses the attached Java implementation as the behavioral and architectural reference, but is an independent implementation rather than a line-by-line translation. The Java reference already establishes TCP networking, RESP handling, GET/SET, TTL, replication, PSYNC/FULLRESYNC plumbing, ACK/WAIT, transactions, Docker, Azure DevOps, Kubernetes and Helm. CacheVault extends that foundation with real RDB snapshots, AOF recovery, WATCH/optimistic locking, eviction, Pub/Sub, Lists, comprehensive tests and benchmarking.

## Architecture

```text
Client
  │ RESP / TCP
  ▼
Server / Connection Management
  │
  ├── RESP parser/serializer
  ├── Command engine
  ├── Transaction + WATCH state
  ├── Pub/Sub broker
  │
  ▼
Concurrent In-Memory Data Store
  │       │
  │       ├── TTL / expiration
  │       ├── LRU / LFU / Random eviction
  │       └── Lists
  │
  ├──────────────┬──────────────────┐
  ▼              ▼                  ▼
AOF            RDB              Replication
replay         snapshots        master → replicas
```

## Supported commands

`PING`, `ECHO`, `GET`, `SET`, `DEL`, `INCR`, `MULTI`, `EXEC`, `DISCARD`, `WATCH`, `INFO replication`, `REPLCONF`, `PSYNC`, `WAIT`, `SUBSCRIBE`, `UNSUBSCRIBE`, `PUBLISH`, `LPUSH`, `RPUSH`, `LPOP`, `RPOP`, `LRANGE`.

`SET` supports the required `PX` expiration option.

## Project structure

```text
CacheVault/
├── src/
│   ├── CacheVault.Server/           TCP server and connection lifecycle
│   ├── CacheVault.Core/             data store, values, TTL, eviction
│   ├── CacheVault.Protocol/         RESP parser/serializer
│   ├── CacheVault.Persistence/      RDB + AOF
│   ├── CacheVault.Replication/      master/replica state and ACKs
│   └── CacheVault.Infrastructure/   command engine and Pub/Sub
├── tests/
│   ├── CacheVault.UnitTests/
│   ├── CacheVault.IntegrationTests/
│   └── CacheVault.EndToEndTests/
├── benchmarks/CacheVault.Benchmarks/
├── deploy/
├── helm/cachevault/
├── .github/workflows/ci.yml
├── Dockerfile
├── docker-compose.yml
└── CacheVault.sln
```

## Build and run

```bash
dotnet restore CacheVault.sln
dotnet build CacheVault.sln -c Release
dotnet test CacheVault.sln -c Release
dotnet run --project src/CacheVault.Server -- --host 0.0.0.0 --port 6379
```

A Redis-compatible client can connect to port `6379`.

## Persistence

### RDB

CacheVault writes a real binary snapshot containing format/version metadata, keys, supported value types, TTL timestamps and entry versions. Snapshots are written to a temporary file and atomically moved into place. The same RDB representation is used for initial replica synchronization.

### AOF

Mutating commands are appended as RESP command arrays. On restart the AOF is parsed and replayed through the command engine without re-appending or re-propagating recovery commands.

## Replication

A master maintains a replication ID and byte offset. Replicas perform the initial `PING` / `REPLCONF` / `PSYNC` handshake, receive `FULLRESYNC` and an RDB snapshot, then consume the master command stream. `REPLCONF ACK`, `GETACK` and `WAIT` provide acknowledgement tracking.

## Transactions and WATCH

Each TCP connection has isolated transaction state. `MULTI` queues commands, `EXEC` evaluates them atomically under the database transaction gate, and `DISCARD` clears the queue. `WATCH` records per-key versions and causes `EXEC` to abort when a watched key changes.

## RESP and networking

The protocol parser treats TCP as a stream: a single read may contain several commands and a command may be split across several reads. Pipelining, nested arrays, bulk lengths, nulls and malformed input are handled at the protocol boundary.

## Eviction

Configure `--maxmemory` and `--maxmemory-policy` with `Lru`, `Lfu`, or `Random`. `NoEviction` is the default.

## Docker

```bash
docker build -t cachevault .
docker compose up --build
```

The Compose topology starts one master and two replicas.

## Kubernetes / Helm

Raw manifests are under `deploy/kubernetes`. The Helm chart is under `helm/cachevault`.

```bash
helm lint helm/cachevault
helm install cachevault helm/cachevault
```

## Configuration

Important options:

```text
--host <address>
--port <port>
--replicaof <master-host> <master-port>
--rdb-path <path>
--aof-path <path>
--no-rdb
--no-aof
--rdb-interval <seconds>
--maxmemory <bytes>
--maxmemory-policy <NoEviction|Lru|Lfu|Random>
--log-level <level>
```

## Testing

The test projects cover data-store concurrency, TTL, lists, RESP partial reads/pipelining and protocol serialization. The solution is structured so replication, persistence, Pub/Sub and full TCP end-to-end tests can exercise the same production command engine.

## Benchmarking

```bash
dotnet run -c Release --project benchmarks/CacheVault.Benchmarks
```

BenchmarkDotNet is used for store-level GET/SET measurements. The benchmarking project is intentionally separate from correctness tests.

## Reference implementation

The original Java repository snapshot is retained as a development reference only. CacheVault does not copy Java source mechanically. Where the Java implementation is simplified—most notably its embedded empty RDB payload—the C# implementation uses the requirements of this project as the source of truth and implements the deeper behavior directly.
