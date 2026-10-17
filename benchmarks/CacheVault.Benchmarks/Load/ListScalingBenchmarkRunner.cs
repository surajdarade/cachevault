using System.Diagnostics;
using System.Globalization;
using System.Text;
using CacheVault.Benchmarks.Infrastructure;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Benchmarks.Load;

/// <summary>
/// Measures single-command TCP round-trip latency for list operations while
/// keeping the list at a fixed target size before every measured command.
/// Mutating commands are restored by an untimed compensating command.
/// </summary>
public static class ListScalingBenchmarkRunner
{
    private const string ListKey = "list:scaling:benchmark";
    private const string ListValue = "vvvvvvvvvvvvvvvv"; // 16 UTF-8 bytes
    private const int SeedBatchSize = 1_000;
    private const int LrangePageSize = 100;

    private static readonly string[] Operations =
    [
        "LPUSH",
        "RPUSH",
        "LPOP",
        "RPOP",
        "LRANGE_0_99"
    ];

    public static async Task RunAsync(string[] args)
    {
        Options options = Options.Parse(args);
        string outputDirectory = Path.Combine(
            "BenchmarkDotNet.Artifacts",
            "ListScaling");
        Directory.CreateDirectory(outputDirectory);

        string timestamp = DateTime.UtcNow.ToString(
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture);
        string outputPath = Path.GetFullPath(Path.Combine(
            outputDirectory,
            $"list-scaling-{timestamp}.csv"));

        Console.WriteLine();
        Console.WriteLine("CacheVault list scaling benchmark");
        Console.WriteLine("---------------------------------");
        Console.WriteLine($"List sizes             : {string.Join(", ", options.Sizes.Select(x => x.ToString("N0", CultureInfo.InvariantCulture)))} items");
        Console.WriteLine($"Trials per size        : {options.Repeats}");
        Console.WriteLine($"Measured samples/op    : {options.Samples} per trial ({options.Samples * options.Repeats:N0} pooled samples per operation and size)");
        Console.WriteLine($"Warm-up operations/op  : {options.Warmup}");
        Console.WriteLine("Clients                : 1 sequential TCP client");
        Console.WriteLine("AOF / RDB              : OFF / OFF");
        Console.WriteLine("Memory limit           : disabled (MaxMemoryBytes=0)");
        Console.WriteLine($"List value size        : {Encoding.UTF8.GetByteCount(ListValue)} UTF-8 bytes");
        Console.WriteLine("LRANGE                  : fixed page LRANGE 0 99 (100 items)");
        Console.WriteLine("Mutation timing         : one command round trip; restoration command excluded from timing");
        Console.WriteLine("Latency sampling        : every measured command; warm-up excluded");
        Console.WriteLine($"Raw per-sample CSV     : {outputPath}");
        Console.WriteLine();

        var allSamples = new Dictionary<(int Size, string Operation), List<double>>();

        await using var csv = new StreamWriter(
            outputPath,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await csv.WriteLineAsync("dataset_size,trial,operation,sample_index,latency_ms")
            .ConfigureAwait(false);
        await csv.FlushAsync().ConfigureAwait(false);

        foreach (int size in options.Sizes)
        {
            Console.WriteLine($"DATASET SIZE: {size:N0} items");
            Console.WriteLine("Trial | Operation     | N    | p50 (ms) | p95 (ms) | p99 (ms) | Max observed (ms)");
            Console.WriteLine("------|---------------|------|----------|----------|----------|------------------");

            for (int trial = 1; trial <= options.Repeats; trial++)
            {
                List<MeasurementSample> trialSamples = await RunTrialAsync(
                    size,
                    trial,
                    options.Samples,
                    options.Warmup).ConfigureAwait(false);

                foreach (MeasurementSample sample in trialSamples)
                {
                    await csv.WriteLineAsync(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4:F6}",
                        sample.DatasetSize,
                        sample.Trial,
                        sample.Operation,
                        sample.SampleIndex,
                        sample.LatencyMilliseconds)).ConfigureAwait(false);

                    var key = (sample.DatasetSize, sample.Operation);
                    if (!allSamples.TryGetValue(key, out List<double>? values))
                    {
                        values = [];
                        allSamples.Add(key, values);
                    }

                    values.Add(sample.LatencyMilliseconds);
                }

                await csv.FlushAsync().ConfigureAwait(false);

                foreach (string operation in Operations)
                {
                    double[] values = trialSamples
                        .Where(x => x.Operation == operation)
                        .Select(x => x.LatencyMilliseconds)
                        .ToArray();
                    PrintSummaryRow(trial, operation, values);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Pooled measured samples for {size:N0} items ({options.Samples * options.Repeats:N0} per operation)");
            Console.WriteLine("Operation     | p50 (ms) | p95 (ms) | p99 (ms) | Max observed (ms)");
            Console.WriteLine("--------------|----------|----------|----------|------------------");

            foreach (string operation in Operations)
            {
                PrintPooledSummaryRow(
                    operation,
                    allSamples[(size, operation)]);
            }

            Console.WriteLine();
        }

        Console.WriteLine("Interpretation:");
        Console.WriteLine("- Every timed command starts with the list at the configured target size.");
        Console.WriteLine("- The compensating operation restores list size and is excluded from the timed interval.");
        Console.WriteLine("- LRANGE is a fixed 100-item page, so result payload size remains constant across list sizes.");
        Console.WriteLine("- Max observed is the maximum among the recorded samples, not a guarantee about all production requests.");
        Console.WriteLine("- Measurements include TCP round-trip and RESP response parsing, not only server-side execution time.");
        Console.WriteLine($"Raw samples saved to: {outputPath}");
    }

    private static async Task<List<MeasurementSample>> RunTrialAsync(
        int listSize,
        int trial,
        int samplesPerOperation,
        int warmupPerOperation)
    {
        await using CacheVaultBenchmarkServer server =
            await CacheVaultBenchmarkServer.StartAsync(
                enableRdb: false,
                enableAof: false,
                bypassCommandExecutionGate: false).ConfigureAwait(false);

        await using BenchmarkRedisClient client =
            await BenchmarkRedisClient.ConnectAsync(server.Endpoint).ConfigureAwait(false);

        await SeedListAsync(client, listSize).ConfigureAwait(false);
        await VerifyListLengthAsync(client, listSize).ConfigureAwait(false);

        byte[] lpush = BenchmarkRedisClient.Command("LPUSH", ListKey, ListValue);
        byte[] rpush = BenchmarkRedisClient.Command("RPUSH", ListKey, ListValue);
        byte[] lpop = BenchmarkRedisClient.Command("LPOP", ListKey);
        byte[] rpop = BenchmarkRedisClient.Command("RPOP", ListKey);
        byte[] lrange = BenchmarkRedisClient.Command("LRANGE", ListKey, "0", "99");

        var timedCommands = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["LPUSH"] = lpush,
            ["RPUSH"] = rpush,
            ["LPOP"] = lpop,
            ["RPOP"] = rpop,
            ["LRANGE_0_99"] = lrange
        };

        var restoreCommands = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["LPUSH"] = rpop,
            ["RPUSH"] = lpop,
            ["LPOP"] = lpush,
            ["RPOP"] = rpush
        };

        // Rotate operation order by trial to avoid always measuring the same
        // command first after seeding and warm-up.
        string[] operationOrder = Operations
            .Skip((trial - 1) % Operations.Length)
            .Concat(Operations.Take((trial - 1) % Operations.Length))
            .ToArray();

        foreach (string operation in operationOrder)
        {
            for (int index = 0; index < warmupPerOperation; index++)
            {
                await ExecuteAndRestoreAsync(
                    client,
                    operation,
                    timedCommands[operation],
                    restoreCommands.GetValueOrDefault(operation),
                    listSize).ConfigureAwait(false);
            }
        }

        var results = new List<MeasurementSample>(
            Operations.Length * samplesPerOperation);

        foreach (string operation in operationOrder)
        {
            for (int index = 1; index <= samplesPerOperation; index++)
            {
                long startTimestamp = Stopwatch.GetTimestamp();
                RespValue response = await client.SendAsync(
                    timedCommands[operation]).ConfigureAwait(false);
                double elapsedMilliseconds = Stopwatch.GetElapsedTime(
                    startTimestamp).TotalMilliseconds;

                ValidateTimedResponse(
                    operation,
                    response,
                    listSize);

                byte[]? restoreCommand = restoreCommands.GetValueOrDefault(operation);
                if (restoreCommand is not null)
                {
                    RespValue restoredResponse = await client.SendAsync(
                        restoreCommand).ConfigureAwait(false);
                    ValidateRestoreResponse(
                        operation,
                        restoredResponse,
                        listSize);
                }

                results.Add(new MeasurementSample(
                    listSize,
                    trial,
                    operation,
                    index,
                    elapsedMilliseconds));
            }
        }

        await VerifyListLengthAsync(client, listSize).ConfigureAwait(false);
        return results;
    }

    private static async Task SeedListAsync(
        BenchmarkRedisClient client,
        int listSize)
    {
        for (int offset = 0; offset < listSize; offset += SeedBatchSize)
        {
            int count = Math.Min(SeedBatchSize, listSize - offset);
            var arguments = new string[count + 2];
            arguments[0] = "RPUSH";
            arguments[1] = ListKey;
            Array.Fill(arguments, ListValue, 2, count);

            RespValue response = await client.SendAsync(
                BenchmarkRedisClient.Command(arguments)).ConfigureAwait(false);
            RequireInteger(
                response,
                offset + count,
                $"RPUSH seed batch ending at item {offset + count}");
        }
    }

    private static async Task VerifyListLengthAsync(
        BenchmarkRedisClient client,
        int expectedLength)
    {
        RespValue response = await client.SendAsync(
            BenchmarkRedisClient.Command("LLEN", ListKey)).ConfigureAwait(false);
        RequireInteger(response, expectedLength, "LLEN verification");
    }

    private static async Task ExecuteAndRestoreAsync(
        BenchmarkRedisClient client,
        string operation,
        byte[] timedCommand,
        byte[]? restoreCommand,
        int listSize)
    {
        RespValue response = await client.SendAsync(timedCommand).ConfigureAwait(false);
        ValidateTimedResponse(operation, response, listSize);

        if (restoreCommand is not null)
        {
            RespValue restoreResponse = await client.SendAsync(restoreCommand).ConfigureAwait(false);
            ValidateRestoreResponse(operation, restoreResponse, listSize);
        }
    }

    private static void ValidateTimedResponse(
        string operation,
        RespValue response,
        int listSize)
    {
        switch (operation)
        {
            case "LPUSH":
            case "RPUSH":
                RequireInteger(response, listSize + 1, operation);
                break;

            case "LPOP":
            case "RPOP":
                RequireBulkString(response, ListValue, operation);
                break;

            case "LRANGE_0_99":
                if (response is not RespArray array ||
                    array.Values is null ||
                    array.Values.Count != LrangePageSize)
                {
                    throw new InvalidOperationException(
                        $"LRANGE returned an unexpected response; expected {LrangePageSize} values at list size {listSize}.");
                }

                foreach (RespValue value in array.Values)
                {
                    RequireBulkString(value, ListValue, operation);
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown list benchmark operation.");
        }
    }

    private static void ValidateRestoreResponse(
        string operation,
        RespValue response,
        int listSize)
    {
        switch (operation)
        {
            case "LPUSH":
            case "RPUSH":
                RequireBulkString(response, ListValue, $"{operation} compensation");
                break;

            case "LPOP":
            case "RPOP":
                RequireInteger(response, listSize, $"{operation} compensation");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "This operation has no compensation command.");
        }
    }

    private static void RequireInteger(
        RespValue response,
        long expected,
        string operation)
    {
        if (response is not RespInteger integer || integer.Value != expected)
        {
            throw new InvalidOperationException(
                $"{operation} returned {response.Type}; expected integer {expected}.");
        }
    }

    private static void RequireBulkString(
        RespValue response,
        string expected,
        string operation)
    {
        if (response is not RespBulkString bulk ||
            !string.Equals(bulk.Value, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{operation} returned an unexpected bulk string response.");
        }
    }

    private static void PrintSummaryRow(
        int trial,
        string operation,
        double[] values)
    {
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0,5} | {1,-13} | {2,4} | {3,8:F4} | {4,8:F4} | {5,8:F4} | {6,16:F4}",
            trial,
            operation,
            values.Length,
            Percentile(values, 50),
            Percentile(values, 95),
            Percentile(values, 99),
            values.Max()));
    }

    private static void PrintPooledSummaryRow(
        string operation,
        IReadOnlyList<double> values)
    {
        double[] data = values.ToArray();
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0,-13} | {1,8:F4} | {2,8:F4} | {3,8:F4} | {4,16:F4}",
            operation,
            Percentile(data, 50),
            Percentile(data, 95),
            Percentile(data, 99),
            data.Max()));
    }

    private static double Percentile(
        IReadOnlyCollection<double> values,
        double percentile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length);
        int index = Math.Clamp(rank - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private sealed record MeasurementSample(
        int DatasetSize,
        int Trial,
        string Operation,
        int SampleIndex,
        double LatencyMilliseconds);

    private sealed record Options(
        int[] Sizes,
        int Repeats,
        int Samples,
        int Warmup)
    {
        public static Options Parse(string[] args)
        {
            int[] sizes = [1_000, 10_000, 100_000];
            int repeats = 5;
            int samples = 100;
            int warmup = 20;

            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for '{option}'.");
                }

                string value = args[++index];
                switch (option)
                {
                    case "--sizes":
                        sizes = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(token => ParsePositiveInt(token, option))
                            .Distinct()
                            .OrderBy(size => size)
                            .ToArray();
                        if (sizes.Length == 0 || sizes.Any(size => size < LrangePageSize))
                        {
                            throw new ArgumentException(
                                $"'{option}' must contain list sizes of at least {LrangePageSize}, separated by commas.");
                        }
                        break;

                    case "--repeats": repeats = ParsePositiveInt(value, option); break;
                    case "--samples": samples = ParsePositiveInt(value, option); break;
                    case "--warmup": warmup = ParsePositiveInt(value, option); break;
                    default: throw new ArgumentException($"Unknown list-scaling benchmark option '{option}'.");
                }
            }

            return new Options(sizes, repeats, samples, warmup);
        }

        private static int ParsePositiveInt(string value, string option)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
            {
                throw new ArgumentException($"'{option}' values must be positive integers.");
            }

            return parsed;
        }
    }
}
