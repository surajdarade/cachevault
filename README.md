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