using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using CacheVault.Benchmarks.Infrastructure;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Benchmarks.Load;

public static class LoadTestRunner
{
    public static async Task RunAsync(string[] args)
    {
        LoadOptions options = LoadOptions.Parse(args);

        Console.WriteLine();
        Console.WriteLine("CacheVault load benchmark");
        Console.WriteLine("--------------------------");
        Console.WriteLine($"Duration      : {options.DurationSeconds}s");
        Console.WriteLine($"Clients       : {options.Clients}");
        Console.WriteLine($"Workload      : {options.Workload}");
        Console.WriteLine($"Pipeline      : {options.PipelineDepth}");
        Console.WriteLine($"Sample rate   : 1/{options.SampleRate}");
        Console.WriteLine($"AOF           : {(options.EnableAof ? "ON" : "OFF")}");
        Console.WriteLine($"RDB           : {(options.EnableRdb ? "ON" : "OFF")}");
        Console.WriteLine($"Command gate  : {(options.BypassCommandExecutionGate ? "BYPASSED (diagnostic)" : "ON")}");
        Console.WriteLine($"Runtime       : {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS            : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"CPU count     : {Environment.ProcessorCount}");
        Console.WriteLine();

        await using CacheVaultBenchmarkServer server =
            await CacheVaultBenchmarkServer.StartAsync(
                options.EnableRdb,
                options.EnableAof,
                options.BypassCommandExecutionGate).ConfigureAwait(false);

        Console.WriteLine($"Server        : {server.Endpoint}");

        await using BenchmarkRedisClient seedClient =
            await BenchmarkRedisClient.ConnectAsync(server.Endpoint).ConfigureAwait(false);

        await SeedAsync(seedClient).ConfigureAwait(false);

        using var cancellation = new CancellationTokenSource();
        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool>[] readySignals = Enumerable.Range(0, options.Clients)
            .Select(_ => new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();

        Task<WorkerResult>[] workers = new Task<WorkerResult>[options.Clients];
        for (int index = 0; index < workers.Length; index++)
        {
            int workerId = index;
            workers[index] = RunWorkerAsync(
                server.Endpoint,
                workerId,
                options,
                startGate.Task,
                readySignals[workerId],
                cancellation.Token);
        }

        Stopwatch total = new();
        try
        {
            // Exclude connection setup and warm-up from the measured interval.
            await Task.WhenAll(readySignals.Select(signal => signal.Task))
                .WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            total.Start();
            startGate.TrySetResult(true);
            await Task.Delay(TimeSpan.FromSeconds(options.DurationSeconds))
                .ConfigureAwait(false);
            cancellation.Cancel();
            total.Stop();

            WorkerResult[] results =
                await Task.WhenAll(workers).ConfigureAwait(false);
            PrintResults(results, total.Elapsed);
        }
        finally
        {
            cancellation.Cancel();
            startGate.TrySetResult(true);
            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch
            {
                // Preserve the original setup/benchmark exception.
            }
        }
    }

    private static async Task SeedAsync(BenchmarkRedisClient client)
    {
        for (int index = 0; index < 1000; index++)
        {
            EnsureType(await client.SendAsync(
                BenchmarkRedisClient.Command(
                    "SET",
                    $"load:key:{index}",
                    "benchmark-value")).ConfigureAwait(false),
                RespValueType.SimpleString, "SET during seeding");
        }

        for (int index = 0; index < 100; index++)
        {
            EnsureType(await client.SendAsync(
                BenchmarkRedisClient.Command(
                    "RPUSH",
                    $"load:list:{index}",
                    "one",
                    "two",
                    "three",
                    "four",
                    "five")).ConfigureAwait(false),
                RespValueType.Integer, "RPUSH during seeding");
        }
    }

    private static async Task<WorkerResult> RunWorkerAsync(
        System.Net.IPEndPoint endpoint,
        int workerId,
        LoadOptions options,
        Task startSignal,
        TaskCompletionSource<bool> readySignal,
        CancellationToken cancellationToken)
    {
        await using BenchmarkRedisClient client =
            await BenchmarkRedisClient.ConnectAsync(
                endpoint,
                cancellationToken).ConfigureAwait(false);

        var result = new WorkerResult();
        Random random = new(unchecked(Environment.TickCount * 31 + workerId));

        byte[][] getRequests = CreateRequests(
            1000,
            keyIndex => ["GET", $"load:key:{keyIndex}"]);

        byte[][] setRequests = CreateRequests(
            1000,
            keyIndex => ["SET", $"load:key:{keyIndex}", "benchmark-value"]);

        byte[][] incrRequests = CreateRequests(
            1000,
            keyIndex => ["INCR", $"load:counter:{keyIndex}"]);

        byte[][] listRequests = CreateRequests(
            100,
            keyIndex => ["LRANGE", $"load:list:{keyIndex}", "0", "-1"]);

        byte[] pipeline = CreatePipeline(options.PipelineDepth);

        try
        {
            // Warm each connection before the synchronized measurement window.
            for (int warmup = 0; warmup < 10; warmup++)
            {
                await ExecuteWorkloadAsync(
                    client, options, random, getRequests, setRequests, incrRequests,
                    listRequests, pipeline, CancellationToken.None).ConfigureAwait(false);
            }
            readySignal.TrySetResult(true);
        }
        catch (Exception exception)
        {
            readySignal.TrySetException(exception);
            throw;
        }

        await startSignal.ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // A pipeline is one in-flight batch with multiple server replies.
                // Once sent, drain that batch even if the measurement deadline fires;
                // canceling the read mid-batch closes the client while replies remain
                // queued, which can surface as an unhandled server-side write exception.
                CancellationToken operationCancellationToken =
                    options.Workload == LoadWorkload.Pipeline
                        ? CancellationToken.None
                        : cancellationToken;

                int commands = await ExecuteWorkloadAsync(
                    client,
                    options,
                    random,
                    getRequests,
                    setRequests,
                    incrRequests,
                    listRequests,
                    pipeline,
                    operationCancellationToken).ConfigureAwait(false);

                stopwatch.Stop();

                // Do not count or sample a batch that was still in flight when the
                // measurement window ended. Its replies have been drained so teardown
                // can close the connection without abandoning queued responses.
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                result.Commands += commands;

                if (result.SampleCounter++ % options.SampleRate == 0)
                {
                    result.LatenciesTicks.Add(stopwatch.ElapsedTicks);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (BenchmarkServerErrorException)
            {
                result.ServerErrors++;
            }
            catch (BenchmarkProtocolException)
            {
                result.ProtocolErrors++;
            }
            catch (IOException)
            {
                result.TransportErrors++;
            }
            catch (SocketException)
            {
                result.TransportErrors++;
            }
            catch (Exception)
            {
                result.OtherErrors++;
            }
        }

        return result;
    }

    private static async Task<int> ExecuteWorkloadAsync(
        BenchmarkRedisClient client,
        LoadOptions options,
        Random random,
        byte[][] getRequests,
        byte[][] setRequests,
        byte[][] incrRequests,
        byte[][] listRequests,
        byte[] pipeline,
        CancellationToken cancellationToken)
    {
        switch (options.Workload)
        {
            case LoadWorkload.Get:
                EnsureType(await client.SendAsync(
                    getRequests[random.Next(getRequests.Length)],
                    cancellationToken).ConfigureAwait(false), RespValueType.BulkString, "GET");
                return 1;

            case LoadWorkload.Set:
                EnsureType(await client.SendAsync(
                    setRequests[random.Next(setRequests.Length)],
                    cancellationToken).ConfigureAwait(false), RespValueType.SimpleString, "SET");
                return 1;

            case LoadWorkload.Mixed:
            {
                int choice = random.Next(100);

                byte[] request =
                    choice < 70
                        ? getRequests[random.Next(getRequests.Length)]
                        : choice < 90
                            ? setRequests[random.Next(setRequests.Length)]
                            : incrRequests[random.Next(incrRequests.Length)];

                RespValue response = await client.SendAsync(
                    request, cancellationToken).ConfigureAwait(false);
                RespValueType expected = choice < 70
                    ? RespValueType.BulkString
                    : choice < 90 ? RespValueType.SimpleString : RespValueType.Integer;
                EnsureType(response, expected, "mixed workload command");

                return 1;
            }

            case LoadWorkload.List:
                EnsureType(await client.SendAsync(
                    listRequests[random.Next(listRequests.Length)],
                    cancellationToken).ConfigureAwait(false), RespValueType.Array, "LRANGE");
                return 1;

            case LoadWorkload.Pipeline:
                IReadOnlyList<RespValue> pipelineResponses = await client.SendPipelineAsync(
                    pipeline, options.PipelineDepth, cancellationToken).ConfigureAwait(false);
                foreach (RespValue response in pipelineResponses)
                {
                    EnsureType(response, RespValueType.BulkString, "pipelined GET");
                }
                return options.PipelineDepth;

            case LoadWorkload.Transaction: {
                    byte[] set =
                        BenchmarkRedisClient.Command(
                            "SET",
                            $"load:transaction:{random.Next(1000)}",
                            "value");

                    byte[] transactionPipeline = BenchmarkRedisClient.Pipeline(
                        [
                            BenchmarkRedisClient.Command("MULTI"),
                    set,
                    BenchmarkRedisClient.Command(
                        "INCR",
                        "load:transaction:counter"),
                    BenchmarkRedisClient.Command("EXEC")
                                ]);

                    IReadOnlyList<RespValue> transactionResponses = await client.SendPipelineAsync(
                        transactionPipeline, 4, cancellationToken).ConfigureAwait(false);
                    EnsureType(transactionResponses[0], RespValueType.SimpleString, "MULTI");
                    EnsureType(transactionResponses[1], RespValueType.SimpleString, "queued SET");
                    EnsureType(transactionResponses[2], RespValueType.SimpleString, "queued INCR");
                    EnsureType(transactionResponses[3], RespValueType.Array, "EXEC");

                    return 4;
                }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void EnsureType(RespValue response, RespValueType expectedType, string operation)
    {
        if (response.Type != expectedType)
        {
            throw new BenchmarkProtocolException(
                $"Unexpected RESP type for {operation}: expected {expectedType}, received {response.Type}.");
        }
    }

    private static byte[][] CreateRequests(
        int count,
        Func<int, string[]> factory)
    {
        var requests = new byte[count][];

        for (int index = 0; index < count; index++)
        {
            requests[index] =
                BenchmarkRedisClient.Command(factory(index));
        }

        return requests;
    }

    private static byte[] CreatePipeline(int depth)
    {
        var commands = new byte[depth][];

        for (int index = 0; index < depth; index++)
        {
            commands[index] =
                BenchmarkRedisClient.Command(
                    "GET",
                    $"load:key:{index % 1000}");
        }

        return BenchmarkRedisClient.Pipeline(commands);
    }

    private static void PrintResults(
        IReadOnlyList<WorkerResult> results,
        TimeSpan elapsed)
    {
        long operations = results.Sum(result => result.Commands);
        long serverErrors = results.Sum(result => result.ServerErrors);
        long protocolErrors = results.Sum(result => result.ProtocolErrors);
        long transportErrors = results.Sum(result => result.TransportErrors);
        long otherErrors = results.Sum(result => result.OtherErrors);
        long errors = serverErrors + protocolErrors + transportErrors + otherErrors;
        long samples = results.Sum(result => result.LatenciesTicks.Count);
        double seconds = elapsed.TotalSeconds;
        double throughput = seconds > 0 ? operations / seconds : 0;

        long[] latencies =
            results
                .SelectMany(result => result.LatenciesTicks)
                .OrderBy(value => value)
                .ToArray();

        Console.WriteLine();
        Console.WriteLine("Results");
        Console.WriteLine("-------");
        Console.WriteLine($"Elapsed        : {seconds:F3}s");
        Console.WriteLine($"Operations     : {operations:N0}");
        Console.WriteLine($"Throughput     : {throughput:N0} ops/sec");
        Console.WriteLine($"Errors         : {errors:N0}");
        Console.WriteLine($"  RESP server  : {serverErrors:N0}");
        Console.WriteLine($"  Protocol     : {protocolErrors:N0}");
        Console.WriteLine($"  Transport    : {transportErrors:N0}");
        Console.WriteLine($"  Other        : {otherErrors:N0}");
        Console.WriteLine($"Latency samples: {samples:N0}");

        if (latencies.Length == 0)
        {
            return;
        }

        Console.WriteLine($"p50            : {ToMilliseconds(Percentile(latencies, 0.50)):F3} ms");
        Console.WriteLine($"p95            : {ToMilliseconds(Percentile(latencies, 0.95)):F3} ms");
        Console.WriteLine($"p99            : {ToMilliseconds(Percentile(latencies, 0.99)):F3} ms");
        Console.WriteLine($"max            : {ToMilliseconds(latencies[^1]):F3} ms");
    }

    private static long Percentile(
        IReadOnlyList<long> sortedTicks,
        double percentile)
    {
        int index =
            (int)Math.Ceiling(percentile * sortedTicks.Count) - 1;

        return sortedTicks[
            Math.Clamp(index, 0, sortedTicks.Count - 1)];
    }

    private static double ToMilliseconds(long ticks) =>
        ticks * 1000.0 / Stopwatch.Frequency;

    private sealed class WorkerResult
    {
        public long Commands { get; set; }

        public long ServerErrors { get; set; }

        public long ProtocolErrors { get; set; }

        public long TransportErrors { get; set; }

        public long OtherErrors { get; set; }

        public long SampleCounter { get; set; }

        public List<long> LatenciesTicks { get; } = [];
    }
}
