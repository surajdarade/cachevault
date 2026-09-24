using System.Diagnostics;
using System.Runtime.InteropServices;
using CacheVault.Benchmarks.Infrastructure;

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
        Console.WriteLine($"Runtime       : {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS            : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"CPU count     : {Environment.ProcessorCount}");
        Console.WriteLine();

        await using CacheVaultBenchmarkServer server =
            await CacheVaultBenchmarkServer.StartAsync(
                options.EnableRdb,
                options.EnableAof).ConfigureAwait(false);

        Console.WriteLine($"Server        : {server.Endpoint}");

        await using BenchmarkRedisClient seedClient =
            await BenchmarkRedisClient.ConnectAsync(server.Endpoint).ConfigureAwait(false);

        await SeedAsync(seedClient).ConfigureAwait(false);

        using var cancellation =
            new CancellationTokenSource(TimeSpan.FromSeconds(options.DurationSeconds));

        Task<WorkerResult>[] workers = new Task<WorkerResult>[options.Clients];
        Stopwatch total = Stopwatch.StartNew();

        for (int index = 0; index < workers.Length; index++)
        {
            int workerId = index;
            workers[index] = RunWorkerAsync(
                server.Endpoint,
                workerId,
                options,
                cancellation.Token);
        }

        WorkerResult[] results =
            await Task.WhenAll(workers).ConfigureAwait(false);

        total.Stop();

        PrintResults(results, total.Elapsed);
    }

    private static async Task SeedAsync(BenchmarkRedisClient client)
    {
        for (int index = 0; index < 1000; index++)
        {
            await client.SendAsync(
                BenchmarkRedisClient.Command(
                    "SET",
                    $"load:key:{index}",
                    "benchmark-value")).ConfigureAwait(false);
        }

        for (int index = 0; index < 100; index++)
        {
            await client.SendAsync(
                BenchmarkRedisClient.Command(
                    "RPUSH",
                    $"load:list:{index}",
                    "one",
                    "two",
                    "three",
                    "four",
                    "five")).ConfigureAwait(false);
        }
    }

    private static async Task<WorkerResult> RunWorkerAsync(
        System.Net.IPEndPoint endpoint,
        int workerId,
        LoadOptions options,
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

        while (!cancellationToken.IsCancellationRequested)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                int commands = await ExecuteWorkloadAsync(
                    client,
                    options,
                    random,
                    getRequests,
                    setRequests,
                    incrRequests,
                    listRequests,
                    pipeline,
                    cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();

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
            catch
            {
                result.Errors++;
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
                await client.SendAsync(
                    getRequests[random.Next(getRequests.Length)],
                    cancellationToken).ConfigureAwait(false);
                return 1;

            case LoadWorkload.Set:
                await client.SendAsync(
                    setRequests[random.Next(setRequests.Length)],
                    cancellationToken).ConfigureAwait(false);
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

                await client.SendAsync(
                    request,
                    cancellationToken).ConfigureAwait(false);

                return 1;
            }

            case LoadWorkload.List:
                await client.SendAsync(
                    listRequests[random.Next(listRequests.Length)],
                    cancellationToken).ConfigureAwait(false);
                return 1;

            case LoadWorkload.Pipeline:
                await client.SendPipelineAsync(
                    pipeline,
                    options.PipelineDepth,
                    cancellationToken).ConfigureAwait(false);
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

                    await client.SendPipelineAsync(
                        transactionPipeline,
                        4,
                        cancellationToken).ConfigureAwait(false);

                    return 4;
                }

            default:
                throw new ArgumentOutOfRangeException();
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
        long errors = results.Sum(result => result.Errors);
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

        public long Errors { get; set; }

        public long SampleCounter { get; set; }

        public List<long> LatenciesTicks { get; } = [];
    }
}
