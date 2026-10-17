# CacheVault Benchmark Report

**Status:** Preliminary, locally measured results\
**Benchmark date:** October 2026\
**Environment:** Windows 10.0.26200; .NET SDK 10.0.301; .NET runtime
10.0.12; Intel Core Ultra 5 135H reported; 18 logical processors
reported\
**Build:** Release\
**Scope:** RESP load tests (including command-gate diagnostics), list mutation scaling, AOF write overhead, and RDB snapshot/restore smoke and scaling tests\
**Important:** Results are from the user's local machine and are not
comparisons against Redis or another datastore.

## 1. Executive summary

The retained benchmark evidence includes several distinct local run sets.
One material inconsistency remains in 64-client GET-only throughput, so the
figures below must not be collapsed into a single baseline:

-   An earlier two-run GET-only set reported approximately 95.0K
    operations/second at 64 clients, with mean-of-run p99 latency around
    2.78 ms. A later five-trial command-gate study on commit `dd277ec`
    measured median throughput of 48.1K ops/s with the gate ON and 47.5K
    ops/s with diagnostic bypass. The earlier set's exact source revision
    and full raw stdout were not retained, so this difference is unresolved.
    Treat 95.0K ops/s as a historical observation, not the current headline
    result, and do not infer the cause from these numbers alone.
-   A 16-client mixed workload (70% GET, 20% SET, 10% INCR) averaged
    77.0K operations/second across two runs.
-   A 16-client, pipeline-depth-100 workload averaged 239.0K
    commands/second across two clean runs. Its latency observations are
    **per batch of 100 commands**, not per individual command.
-   Enabling AOF in the tested 16-client SET workload reduced mean
    throughput from 86.6K to 26.1K operations/second, approximately a
    69.9% reduction. This is a preliminary comparison of separate runs,
    not a controlled multi-pair experiment.
-   RDB file size scaled approximately linearly across the three tested
    dataset sizes. Median snapshot and restore timings increased with
    dataset size, but there was noticeable run-to-run variation.
-   In the selected three-run list-scaling comparison at 100K items, median run-level latency fell 87.5%–91.2% at p50, 86.8%–92.0% at p95, and 85.7%–91.1% at p99 for LPUSH/RPUSH/LPOP/RPOP after incremental list-value memory accounting. For the fixed `LRANGE 0–99` request, the corresponding reductions were 11.5%, 27.9%, and 18.2%. These are local measurements from the selected captures, not general guarantees for all list workloads.
-   A five-trial-per-condition command-gate diagnostic produced mixed results. At 64 clients, bypassing the gate did not improve median throughput, and p95/p99 were lower with the gate ON in all five same-trial comparisons. At 16 clients, bypass throughput median was higher but variability was substantial. The evidence does not justify removing the gate.
-   The current managed-heap delta metric is too noisy to represent
    dataset memory consumption and should not be used as a headline
    result.

No claim is made that these results establish production readiness,
Redis equivalence, durability under power loss, or a performance ranking
against other databases.

## 2. Environment and measurement conventions

Environment information available from the benchmark notes:

-   OS: Windows 10.0.26200
-   .NET SDK: 10.0.301
-   .NET runtime: 10.0.12
-   CPU: Intel Core Ultra 5 135H reported
-   Processor count: 18 reported
-   Build configuration: Release

The precise hardware power profile, background workload, thermal state,
and full machine configuration were not recorded with every trial.
Results should therefore be treated as local measurements rather than
universal performance guarantees.

Unless explicitly noted, the load tests used AOF OFF and RDB OFF. The
server's benchmark configuration used `MaxMemoryBytes=0` (NoEviction).

Latency percentiles shown as averages below are arithmetic averages of
the individual run percentiles, **not percentiles computed from a pooled
set of all requests**.

## 3. RESP load-test results

### 3.1 GET-only workload

All runs used AOF OFF and RDB OFF, with a 60-second measurement window.

  ----------------------------------------------------------------------------
    Clients      Run   Throughput p50 (ms) p95 (ms) p99 (ms) Max (ms)   Errors
                          (ops/s)
  --------- -------- ------------ -------- -------- -------- -------- --------
          1        1       19,280    0.043    0.097    0.147    0.821        0

         16        1      104,470    0.136    0.199    0.279   45.634        0

         16        2       86,461    0.165    0.245    0.416   16.533        0

         16        3       86,562    0.161    0.241    0.442   32.102        0

         64        1       95,886    0.563    0.844    2.741   40.458        0

         64        2       94,169    0.582    0.907    2.826   25.873        0
  ----------------------------------------------------------------------------

Summary: - The earlier two-run 64-client set averaged approximately
**95,028 ops/s**. The later five-trial command-gate study on commit
`dd277ec` measured **45,367–50,878 ops/s** with the gate ON. These results
differ materially. The earlier run set's exact source revision and full
raw stdout were not retained, so the discrepancy cannot be diagnosed from
the available evidence. Treat 95,028 ops/s as historical, not as a verified
current baseline. - Two earlier 16-client runs were approximately
**86.5K ops/s**; the earlier 104,470 ops/s run was materially higher. - In
the earlier dataset, increasing clients from 16 to 64 did not increase
throughput, while p50/p95/p99 latency increased. This suggests additional
contention or saturation may be present, but these measurements alone do
not isolate its cause. - The global `CommandExecutionGate` serializes
command dispatch in the current implementation. This is a source-level
design fact and a plausible bottleneck, not proof that it alone explains
the measured throughput.

### 3.2 Mixed workload

Configuration: 16 clients, 60 seconds, non-pipelined; 70% GET, 20% SET,
10% INCR; AOF OFF and RDB OFF.

  ------------------------------------------------------------------------------
         Run   Throughput   p50 (ms)   p95 (ms)   p99 (ms)   Max (ms)     Errors
                  (ops/s)
  ---------- ------------ ---------- ---------- ---------- ---------- ----------
           1       77,960      0.170      0.270      0.448     29.402          0

           2       76,084      0.176      0.272      0.478     36.948          0

     Mean of       77,022      0.173      0.271      0.463        ---          0
        runs
  ------------------------------------------------------------------------------

The percentile summary is an arithmetic average of per-run percentiles.
It is not a pooled request-level percentile.

### 3.3 Pipeline workload

Configuration: 16 clients, pipeline depth 100, 60 seconds, AOF OFF and
RDB OFF. The runner was updated to drain in-flight pipelines at shutdown
and exit cleanly.

  --------------------------------------------------------------------------------
         Run     Throughput   p50 (ms)   p95 (ms)   p99 (ms)   Max (ms)     Errors
               (commands/s)
  ---------- -------------- ---------- ---------- ---------- ---------- ----------
           1        237,404      5.337     11.881     18.308    330.159          0

           2        240,580      5.617     12.799     17.448    312.224          0

     Mean of        238,992      5.477     12.340     17.878        ---          0
        runs
  --------------------------------------------------------------------------------

**Critical interpretation:** latency observations are measured per
pipeline batch of 100 commands. Do not present these percentiles as
individual-command latency. The maximum batch latency exceeded 300 ms in
both runs, despite much lower p99 values.

A pre-fix run printed results but then terminated with an unhandled
`IOException` during teardown. After the benchmark-only shutdown fix,
two runs exited cleanly with zero reported errors.

### 3.4 SET workload: persistence disabled

Configuration: 16 clients, 60 seconds, AOF OFF and RDB OFF.

  ------------------------------------------------------------------------------
         Run   Throughput   p50 (ms)   p95 (ms)   p99 (ms)   Max (ms)     Errors
                  (ops/s)
  ---------- ------------ ---------- ---------- ---------- ---------- ----------
           1       83,757      0.154      0.275      0.633     32.967          0

           2       89,491      0.149      0.230      0.469     29.528          0

     Mean of       86,624      0.152      0.253      0.551        ---          0
        runs
  ------------------------------------------------------------------------------

### 3.5 SET workload: AOF enabled

Configuration: 16 clients, 60 seconds, AOF ON and RDB OFF.

  ------------------------------------------------------------------------------
         Run   Throughput   p50 (ms)   p95 (ms)   p99 (ms)   Max (ms)     Errors
                  (ops/s)
  ---------- ------------ ---------- ---------- ---------- ---------- ----------
           1       24,371      0.603      0.903      2.093     31.593          0

           2       27,824      0.523      0.788      1.646     16.206          0

     Mean of       26,098      0.563      0.846      1.870        ---          0
        runs
  ------------------------------------------------------------------------------

Comparison of mean throughput: - AOF OFF: 86,624 ops/s - AOF ON: 26,098
ops/s - Observed reduction: approximately **69.9%**; AOF-enabled
throughput was approximately **30.1%** of the baseline.

This is a preliminary two-run comparison, not a randomized or
interleaved paired experiment. Run-to-run variation exists in both
conditions, so the percentage should be reported as an observed local
result rather than a universal AOF penalty.

Source inspection found that the AOF writer serializes appends and calls
`FlushAsync()` after each append. `FlushAsync()` is not, by itself, a
guarantee that data has been physically committed to stable media via
`fsync`/equivalent. Do not claim crash- or power-loss durability without
additional implementation inspection and failure testing.

### 3.6 Command-execution gate diagnostic (GET-only)

**Run set:** `BenchmarkDotNet.Artifacts/CommandGateStudy-20261017-192633/` on the local Windows machine. Five trials per condition at each client count; each run measured for 60 seconds, workload GET-only, sampling configured at 1 in 100, AOF/RDB disabled. The command-gate-bypass condition is diagnostic only. All 20 runs exited with code 0 and reported zero errors.

The table uses the **median across the five run-level reported metrics** for each condition. It does not merge the sampled observations from different runs into a single request-level distribution.

| Clients | Condition | Median throughput (ops/s) | Median p50 (ms) | Median p95 (ms) | Median p99 (ms) | Throughput range (ops/s) | p99 range (ms) |
|---:|---|---:|---:|---:|---:|---:|---:|
| 16 | Gate ON | 50,797 | 0.254 | 0.453 | 1.850 | 48,350–79,321 | 0.866–2.053 |
| 16 | Bypass (diagnostic) | 73,770 | 0.177 | 0.336 | 1.266 | 51,978–74,975 | 1.167–2.094 |
| 64 | Gate ON | 48,113 | 1.073 | 2.463 | 5.861 | 45,367–50,878 | 5.031–6.376 |
| 64 | Bypass (diagnostic) | 47,511 | 1.060 | 2.862 | 6.851 | 44,142–53,484 | 6.071–8.365 |

#### All individual command-gate trials

| Clients | Trial | Condition | Throughput (ops/s) | p50 (ms) | p95 (ms) | p99 (ms) | Errors |
|---:|---:|---|---:|---:|---:|---:|---:|
| 16 | 1 | Gate ON | 48,350 | 0.256 | 0.493 | 2.053 | 0 |
| 16 | 1 | Bypass | 51,978 | 0.240 | 0.443 | 2.094 | 0 |
| 16 | 2 | Gate ON | 79,321 | 0.167 | 0.284 | 0.866 | 0 |
| 16 | 2 | Bypass | 74,442 | 0.166 | 0.336 | 1.238 | 0 |
| 16 | 3 | Gate ON | 74,460 | 0.177 | 0.313 | 1.025 | 0 |
| 16 | 3 | Bypass | 74,975 | 0.169 | 0.298 | 1.266 | 0 |
| 16 | 4 | Gate ON | 50,797 | 0.255 | 0.453 | 1.850 | 0 |
| 16 | 4 | Bypass | 73,770 | 0.177 | 0.308 | 1.167 | 0 |
| 16 | 5 | Gate ON | 49,569 | 0.254 | 0.497 | 2.032 | 0 |
| 16 | 5 | Bypass | 54,061 | 0.241 | 0.421 | 1.737 | 0 |
| 64 | 1 | Gate ON | 49,816 | 1.060 | 2.175 | 5.031 | 0 |
| 64 | 1 | Bypass | 53,484 | 0.952 | 2.184 | 6.071 | 0 |
| 64 | 2 | Gate ON | 50,878 | 1.028 | 2.228 | 5.291 | 0 |
| 64 | 2 | Bypass | 48,373 | 1.033 | 2.862 | 6.851 | 0 |
| 64 | 3 | Gate ON | 46,752 | 1.105 | 2.583 | 6.132 | 0 |
| 64 | 3 | Bypass | 44,142 | 1.084 | 3.472 | 8.365 | 0 |
| 64 | 4 | Gate ON | 45,367 | 1.132 | 2.876 | 6.376 | 0 |
| 64 | 4 | Bypass | 47,019 | 1.060 | 3.107 | 7.317 | 0 |
| 64 | 5 | Gate ON | 48,113 | 1.073 | 2.463 | 5.861 | 0 |
| 64 | 5 | Bypass | 47,511 | 1.074 | 2.784 | 6.738 | 0 |

#### Interpretation and limits

- At 16 clients, bypass median throughput is 45.2% higher than the gate-on median, but the trial ranges overlap substantially. Gate-on p95 was higher than bypass p95 in four of five same-trial comparisons; p99 was higher in two of five. The result is variable and does not isolate causality reliably.
- At 64 clients, bypass median throughput is about 1.3% lower than gate-on throughput. Gate-on p95 and p99 were lower in all five same-trial comparisons. These observations do not support removing the gate for a performance win.
- The log summaries supplied for this study did not include a maximum observed latency or sample count. Do not report an exhaustive maximum or treat the sampled percentiles as covering every request.
- **Unresolved cross-run discrepancy:** the earlier 64-client GET-only results in §3.1 reported 94,169 and 95,886 ops/s, while this study's gate-ON trials ranged from 45,367 to 50,878 ops/s. The current study's captured commit is `dd277ec`; the earlier run set has no verified commit identifier or retained full raw stdout. The source of the difference is unknown. Do not merge these sets into one baseline or attribute the difference to the gate without comparable provenance.
- The experiment is GET-only with persistence disabled. It does not establish safety or behavior for writes, transactions, persistence, replication, or arbitrary concurrency. Bypass mode must remain diagnostic-only.
- The study's full logs, manifest, and captured environment metadata should be preserved from `BenchmarkDotNet.Artifacts/CommandGateStudy-20261017-192633/` alongside this report.

### 3.7 List scaling: incremental list-value memory accounting

A custom TCP benchmark compared the pre-optimization and optimized versions at list sizes of 1K, 10K, and 100K items. The 100K table below summarizes the median of each benchmark invocation's pooled-sample p50/p95/p99 values across three invocations per version. Each invocation used five repeats of 500 samples per operation/size (2,500 samples per operation/size), a single sequential TCP client, 16-byte UTF-8 values, a fixed `LRANGE 0–99` page, and no eviction pressure (`MaxMemoryBytes=0`). For mutation commands, each measured mutation is one TCP round trip; its compensating operation is not included in the measured latency.

| Operation (100K items) | Baseline p50 / p95 / p99 (ms) | Optimized p50 / p95 / p99 (ms) | p50 reduction | p95 reduction | p99 reduction |
|---|---:|---:|---:|---:|---:|
| LPUSH | 0.7068 / 1.4045 / 1.6447 | 0.0771 / 0.1409 / 0.2014 | 89.1% | 90.0% | 87.8% |
| RPUSH | 0.6240 / 1.2389 / 1.4923 | 0.0565 / 0.1115 / 0.1678 | 90.9% | 91.0% | 88.8% |
| LPOP | 0.6582 / 1.4984 / 1.7650 | 0.0820 / 0.1977 / 0.2522 | 87.5% | 86.8% | 85.7% |
| RPOP | 0.5970 / 1.4683 / 1.7703 | 0.0524 / 0.1174 / 0.1574 | 91.2% | 92.0% | 91.1% |
| LRANGE 0–99 | 0.0716 / 0.1746 / 0.2506 | 0.0634 / 0.1258 / 0.2049 | 11.5% | 27.9% | 18.2% |

The mutation-latency reductions are consistent with eliminating a full-list UTF-8 byte-count recalculation from push/pop accounting. They do not eliminate the O(n) element movement caused by inserting/removing index zero in `List<string>`. The selected summary also shows lower run-level percentile medians for the fixed `LRANGE 0–99` request (p50/p95/p99 reductions of 11.5%/27.9%/18.2%); this is a separate observation, not evidence that the memory-accounting change specifically optimized range retrieval.

There is notable run-to-run variation. The recomputed table uses three baseline CSVs—`list-scaling-20261017T114431Z.csv`, `list-scaling-20261017T123748Z.csv`, and `list-scaling-20261017T124746Z.csv`—and three optimized CSVs—`list-scaling-20261017T123421Z.csv`, `list-scaling-20261017T124547Z.csv`, and `list-scaling-20261017T124953Z.csv`. The comparison values above were recomputed from these exact six captures; the prior draft's optimized values did not match the filenames stated for that table. The baseline worktree was observed at commit `ae5f38e`. Individual source commit IDs were not embedded in the CSVs, so source revision provenance for each benchmark invocation is not independently verifiable from the CSV data alone. Two additional optimized runs after the concurrency fix, `list-scaling-20261017T134823Z.csv` and `list-scaling-20261017T135011Z.csv`, are sanity checks and are not included in the three-run median table. The separate baseline worktree also contains `list-scaling-20261017T125346Z.csv`; preserve it rather than mixing it into the stated comparison without recomputing the summary. Since eviction was disabled, these measurements do not quantify behavior under memory pressure or eviction.

## 4. RDB benchmark results

Configuration: Release; AOF OFF; RDB enabled; 64 UTF-8 bytes per string
value; 10 list items per list; number of list keys is
`max(1, keys / 1000)`. Snapshot time is graceful shutdown through
server-task completion. Restore time is server startup through TCP
endpoint readiness. The runner samples three string keys (first,
midpoint, last) and verifies the first list; it does not verify every
restored key.

### 4.1 Individual trials

  -------------------------------------------------------------------------------
        Keys      Trial  Seed (ms)   Shutdown +   File size Restore to    Managed
                                       snapshot     (bytes) ready (ms) heap delta
                                           (ms)                           (bytes)
  ---------- ---------- ---------- ------------ ----------- ---------- ----------
       1,000          1      58.89        10.63      78,988       3.46        920

       1,000          2      29.61         2.20      78,988       1.59    -43,560

       1,000          3      27.91         1.78      78,988       1.44    -43,648

       1,000          4      26.19         1.58      78,988       1.34    -35,064

       1,000          5      26.27         2.02      78,988       1.53    -43,648

      10,000          1     281.04        22.68     799,735      31.57    113,936

      10,000          2     554.48         5.02     799,735      20.14     69,456

      10,000          3     242.32         5.68     799,735      20.00     81,656

      10,000          4     261.47         5.44     799,735      17.85     69,368

      10,000          5     203.03        16.63     799,735      16.00     69,456

      50,000          1   2,807.64        47.29   4,043,095      88.53    126,224

      50,000          2   2,054.87        30.63   4,043,095      81.09     69,456

      50,000          3   1,685.21        29.81   4,043,095      72.19     69,368

      50,000          4   1,930.17        26.25   4,043,095      58.59     69,368

      50,000          5   1,943.60        37.56   4,043,095      66.83     76,664
  -------------------------------------------------------------------------------

### 4.2 Median summary

  Metric                  1,000 keys   10,000 keys   50,000 keys
  --------------------- ------------ ------------- -------------
  Seed time                 27.91 ms     261.47 ms   1,943.60 ms
  Shutdown + snapshot        2.02 ms       5.68 ms      30.63 ms
  RDB file size             78,988 B     799,735 B   4,043,095 B
  Restore to ready           1.53 ms      20.00 ms      72.19 ms
  Managed heap delta       -43,560 B      69,456 B      69,456 B

The median managed-heap delta is shown only for completeness and is
**not a reliable measure of dataset memory**. Garbage collection and
runtime allocations affect this in-process before/after metric; the
negative value at 1,000 keys and non-proportional values at larger sizes
demonstrate its limitations. Exclude it from headline performance
claims.

### 4.3 Interpretation and limitations

-   Snapshot file size grows approximately linearly across these three
    dataset sizes. From 1,000 to 50,000 keys, file size increases by
    about 51.2× for 50× as many string keys.
-   Median snapshot-plus-shutdown time grows from 2.02 ms at 1,000 keys
    to 30.63 ms at 50,000 keys.
-   Median restore-to-ready time grows from 1.53 ms to 72.19 ms over the
    same range.
-   There is meaningful run-to-run variability, particularly in snapshot
    and restore times. All runs are retained; no slow trial was silently
    discarded.
-   Snapshot time includes graceful shutdown overhead. Restore time
    includes listener startup and only sampled data verification is
    performed by the runner.
-   These tests validate a benchmark path and sampled restoration for
    these runs. They do not, by themselves, prove every key was restored
    correctly or establish crash/power-loss recovery behavior.

## 5. Persistence correctness evidence

### AOF

User-reported targeted AOF test run: - 14 total; 14 passed; 0 failed; 0
skipped. - Covers graceful restart/replay, command reader/replayer
behavior, and expiration-related tests in the selected filter. - The
restart integration test was expanded to include list recovery and a
second restart after additional mutations, and the user reported the
targeted test passed.

This is evidence for the tested graceful-restart scenarios. It is not
proof of recovery from abrupt process termination, power loss, disk-full
conditions, or every possible truncated/corrupt AOF tail.

### RDB

User-reported test evidence: - RDB restart integration test passed five
consecutive runs after the shutdown-save fix. - RDB-focused test group:
163 passed, 0 failed, 0 skipped. - User subsequently reported the full
solution test suite passed; exact total count was not provided.

The RDB shutdown defect had been reproduced as concurrent writes to
`cachevault.rdb.tmp` during overlapping shutdown/startup cleanup. The
user approved a production fix that shares the final-save task between
cleanup paths. Targeted tests then passed. Do not imply the fix proves
all shutdown/restart races are impossible; it addresses the reproduced
duplicate-save path.

## 6. Implementation-level limitations to understand

These are source-level observations, not all independently measured
performance causes:

1.  **Global command serialization:** `CommandExecutionGate` is a
    singleton `SemaphoreSlim(1,1)` used around dispatch and applicable
    persistence/replication work. It can serialize otherwise independent
    commands and is a plausible throughput bottleneck. Removing it
    without preserving transaction atomicity would be unsafe.
2.  **Eviction access overhead:** successful reads call
    `EvictionManager.OnAccess()`, which takes a shared lock and updates
    access metadata, including when the policy is NoEviction.
3.  **Eviction candidate selection:** LRU/LFU selection scans entries
    under a lock, which is O(N) in the current implementation.
4.  **List complexity:** list head insertion/removal uses `List<T>`
    operations at index zero, which are O(n); list mutation also
    recalculates UTF-8 byte sizes over all list items.
5.  **AOF flush semantics:** `FlushAsync()` is called per append, but
    this does not itself prove physical-media durability.
6.  **Benchmark scope:** current load-test measurements use a specific
    synthetic workload and local machine. They do not represent every
    command, key distribution, network topology, persistence mode, or
    multi-client production pattern.
7.  **RDB memory metric:** current managed-heap delta is not suitable
    for claims about total dataset memory, peak RSS, or memory overhead.

Do not change production code solely to address these observations
within the current scope freeze. Any change needs a specific correctness
issue, evidence, and explicit approval.

## 7. Recommended claims and claims to avoid

### Supported, with qualification

-   "Measured CacheVault RESP throughput and latency on a local
    Windows/.NET environment across GET-only, mixed, pipelined, and SET
    workloads."
-   "Observed approximately 239K commands/s in a 16-client
    pipeline-depth-100 test; latency was measured per 100-command
    batch."
-   "Observed approximately 70% lower SET throughput with AOF enabled in
    a preliminary two-run comparison."
-   "Measured RDB snapshot file size and snapshot/restore-to-ready
    timings at 1K, 10K, and 50K keys."
-   "Added/ran persistence regression tests for the reproduced RDB
    shutdown-save race and expanded AOF list-recovery coverage," subject
    to matching the actual patch history and test evidence.

### Not supported by current evidence

-   "Redis-equivalent" or "faster than Redis."
-   "Production-ready" or "fault tolerant under power loss."
-   "Guaranteed durable AOF writes."
-   "RDB restore time is pure deserialization time."
-   "Memory overhead is X bytes per key" based on current managed-heap
    deltas.
-   "Peak capacity is X operations/s" from these limited workloads.
-   Treating pipeline-batch latency as individual-command latency.
-   Treating source-level performance concerns as proven causes without
    controlled profiling.

## 8. Reproducibility checklist

For future benchmark runs, record: - Commit hash and clean/dirty
working-tree state. - Exact command line, Release build identity,
SDK/runtime, OS, CPU, RAM, power profile, and background workload. -
Workload mix, number of clients, pipeline depth, duration, persistence
settings, dataset shape, value size, and error counts. - Every trial,
including slow runs; report median and spread. - Whether latency is per
command, per batch, or sampled. - For persistence, clearly define
flush/sync guarantees, shutdown behavior, and what restore verification
covers. - Keep diagnostic-only benchmark patches separate from
production changes and label any bypass mode as non-production.

## 9. Current conclusion

The current evidence establishes a useful **local benchmark baseline**,
plus targeted regression evidence for the AOF and RDB recovery scenarios
described above. It does not establish comparative performance or broad
durability guarantees.

Recommended next work under the existing scope freeze: 1. Preserve this
report and raw command-gate/list-scaling trial outputs in the repository,
retaining every run. 2. Keep the 95K ops/s earlier GET-only result labelled
historical until its source revision and measurement provenance can be
matched; the later five-trial study on `dd277ec` is the better documented
current result set. The discrepancy remains unexplained. 3. Do not remove
the command gate: bypass showed no throughput benefit at 64 clients, and
remains diagnostic-only. 4. If transaction performance is a required
claim, prioritize a transaction-load benchmark rather than speculative
optimization. 5. Improve measurement completeness by capturing sample
counts and maximum sampled latency consistently, and label p50/p95/p99 as
sampled request or batch latency. 6. Only reopen production code changes
if a concrete correctness/reliability issue or a necessary validation gap
is identified and approved.
