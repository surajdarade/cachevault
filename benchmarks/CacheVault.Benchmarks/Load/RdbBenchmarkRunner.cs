using System.Diagnostics;
using System.Globalization;
using System.Net;
using CacheVault.Benchmarks.Infrastructure;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.Benchmarks.Load;

/// <summary>
/// Measures graceful-shutdown RDB snapshot time and startup restore time.
/// Seeding and verification are outside the measured intervals.
/// </summary>
public static class RdbBenchmarkRunner
{
    private const int BatchSize = 100;
    private const int ValueLength = 64;
    private const int ListItems = 10;

    public static async Task RunAsync(string[] args)
    {
        (int keys, int repeats) = Parse(args);

        Console.WriteLine();
        Console.WriteLine("CacheVault RDB persistence benchmark");
        Console.WriteLine("------------------------------------");
        Console.WriteLine($"String keys       : {keys:N0}");
        Console.WriteLine($"List keys         : {GetListCount(keys):N0}");
        Console.WriteLine($"Value size        : {ValueLength} UTF-8 bytes per string value");
        Console.WriteLine($"List items        : {ListItems} per list");
        Console.WriteLine($"Repeats           : {repeats}");
        Console.WriteLine("AOF               : OFF");
        Console.WriteLine("Snapshot timing   : graceful shutdown through server task completion");
        Console.WriteLine("Restore timing    : server StartAsync through TCP endpoint readiness");
        Console.WriteLine("Memory metric     : approximate retained managed-heap delta after restore");
        Console.WriteLine();

        var results = new List<TrialResult>(repeats);
        for (int trial = 1; trial <= repeats; trial++)
        {
            TrialResult result = await RunTrialAsync(keys, trial).ConfigureAwait(false);
            results.Add(result);
            PrintTrial(result);
        }

        Console.WriteLine();
        Console.WriteLine("Median across trials");
        Console.WriteLine("--------------------");
        Console.WriteLine($"Seed time         : {Median(results.Select(x => x.SeedMilliseconds)):F2} ms (informational; excluded from snapshot/restore)");
        Console.WriteLine($"Shutdown + snapshot: {Median(results.Select(x => x.SnapshotMilliseconds)):F2} ms");
        Console.WriteLine($"RDB file size     : {Median(results.Select(x => (double)x.FileBytes)):N0} bytes");
        Console.WriteLine($"Restore to ready  : {Median(results.Select(x => x.RestoreMilliseconds)):F2} ms");
        Console.WriteLine($"Managed heap delta: {Median(results.Select(x => (double)x.ManagedHeapDeltaBytes)):N0} bytes (approximate)");
        Console.WriteLine();
        Console.WriteLine("Interpretation: restore time includes recovery plus listener startup; snapshot time includes graceful shutdown overhead.");
        Console.WriteLine("The managed-heap delta is an in-process estimate, not peak RSS or total dataset memory.");
    }

    private static async Task<TrialResult> RunTrialAsync(int keys, int trial)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CacheVaultRdbBenchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string rdbPath = Path.Combine(directory, "cachevault.rdb");
        double seedMilliseconds = 0;
        double snapshotMilliseconds = 0;
        double restoreMilliseconds = 0;
        long fileBytes = 0;
        long managedHeapDeltaBytes = 0;

        try
        {
            await using (ServerInstance initial = new(rdbPath))
            {
                await initial.StartAsync().ConfigureAwait(false);
                var seedTimer = Stopwatch.StartNew();
                await SeedAsync(initial.Endpoint, keys).ConfigureAwait(false);
                seedTimer.Stop();
                seedMilliseconds = seedTimer.Elapsed.TotalMilliseconds;

                var snapshotTimer = Stopwatch.StartNew();
                await initial.StopAsync().ConfigureAwait(false);
                snapshotTimer.Stop();
                snapshotMilliseconds = snapshotTimer.Elapsed.TotalMilliseconds;
            }

            if (!File.Exists(rdbPath))
            {
                throw new InvalidOperationException("RDB snapshot was not created during graceful shutdown.");
            }

            fileBytes = new FileInfo(rdbPath).Length;
            if (fileBytes <= 0)
            {
                throw new InvalidOperationException("RDB snapshot is empty.");
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long heapBeforeRestore = GC.GetTotalMemory(forceFullCollection: true);

            await using (ServerInstance restored = new(rdbPath))
            {
                var restoreTimer = Stopwatch.StartNew();
                await restored.StartAsync().ConfigureAwait(false);
                restoreTimer.Stop();
                restoreMilliseconds = restoreTimer.Elapsed.TotalMilliseconds;

                await VerifyRestoredDataAsync(restored.Endpoint, keys).ConfigureAwait(false);
                long heapAfterRestore = GC.GetTotalMemory(forceFullCollection: true);
                managedHeapDeltaBytes = heapAfterRestore - heapBeforeRestore;
                await restored.StopAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Cleanup failure must not hide a benchmark result or primary exception.
            }
        }

        return new TrialResult(trial, keys, seedMilliseconds, snapshotMilliseconds, fileBytes, restoreMilliseconds, managedHeapDeltaBytes);
    }

    private static async Task SeedAsync(IPEndPoint endpoint, int keys)
    {
        await using BenchmarkRedisClient client = await BenchmarkRedisClient.ConnectAsync(endpoint).ConfigureAwait(false);
        string value = new('v', ValueLength);

        for (int first = 0; first < keys; first += BatchSize)
        {
            int count = Math.Min(BatchSize, keys - first);
            byte[][] commands = new byte[count][];
            for (int offset = 0; offset < count; offset++)
            {
                int keyIndex = first + offset;
                commands[offset] = BenchmarkRedisClient.Command("SET", $"rdb:key:{keyIndex}", value);
            }

            IReadOnlyList<RespValue> responses = await client.SendPipelineAsync(
                BenchmarkRedisClient.Pipeline(commands),
                count).ConfigureAwait(false);
            ValidatePipelineResponseTypes(
                responses,
                count,
                RespValueType.SimpleString,
                "SET");
        }

        int listCount = GetListCount(keys);
        for (int first = 0; first < listCount; first += BatchSize)
        {
            int count = Math.Min(BatchSize, listCount - first);
            byte[][] commands = new byte[count][];
            for (int offset = 0; offset < count; offset++)
            {
                int listIndex = first + offset;
                string[] command = new string[ListItems + 2];
                command[0] = "RPUSH";
                command[1] = $"rdb:list:{listIndex}";
                for (int item = 0; item < ListItems; item++)
                {
                    command[item + 2] = $"item-{item}";
                }

                commands[offset] = BenchmarkRedisClient.Command(command);
            }

            IReadOnlyList<RespValue> responses = await client.SendPipelineAsync(
                BenchmarkRedisClient.Pipeline(commands),
                count).ConfigureAwait(false);
            ValidatePipelineResponseTypes(
                responses,
                count,
                RespValueType.Integer,
                "RPUSH");
        }
    }

    private static async Task VerifyRestoredDataAsync(IPEndPoint endpoint, int keys)
    {
        await using BenchmarkRedisClient client = await BenchmarkRedisClient.ConnectAsync(endpoint).ConfigureAwait(false);
        string expectedValue = new('v', ValueLength);
        int[] indexes = [0, keys / 2, keys - 1];

        foreach (int index in indexes.Distinct())
        {
            RespValue response = await client.SendAsync(
                BenchmarkRedisClient.Command("GET", $"rdb:key:{index}")).ConfigureAwait(false);
            if (response is not RespBulkString bulk || bulk.Value != expectedValue)
            {
                throw new InvalidOperationException($"RDB restore verification failed for string key index {index}.");
            }
        }

        RespValue listResponse = await client.SendAsync(
            BenchmarkRedisClient.Command("LRANGE", "rdb:list:0", "0", "-1")).ConfigureAwait(false);
        if (listResponse is not RespArray array || array.Values is null || array.Values.Count != ListItems)
        {
            throw new InvalidOperationException("RDB restore verification failed for the first list's item count.");
        }

        for (int index = 0; index < ListItems; index++)
        {
            if (array.Values[index] is not RespBulkString item || item.Value != $"item-{index}")
            {
                throw new InvalidOperationException($"RDB restore verification failed for list item {index}.");
            }
        }
    }

    private static void ValidatePipelineResponseTypes(
        IReadOnlyList<RespValue> responses,
        int expectedCount,
        RespValueType expectedType,
        string commandName)
    {
        if (responses.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"{commandName} pipeline returned {responses.Count} responses; expected {expectedCount}.");
        }

        for (int index = 0; index < responses.Count; index++)
        {
            if (responses[index].Type != expectedType)
            {
                throw new InvalidOperationException(
                    $"{commandName} pipeline response {index} had type {responses[index].Type}; expected {expectedType}.");
            }
        }
    }

    private static int GetListCount(int keys) => Math.Max(1, keys / 1000);

    private static (int Keys, int Repeats) Parse(string[] args)
    {
        int keys = 10_000;
        int repeats = 3;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for '{option}'.");
            }

            string value = args[++index];
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
            {
                throw new ArgumentException($"'{option}' must be a positive integer.");
            }

            switch (option)
            {
                case "--keys": keys = parsed; break;
                case "--repeats": repeats = parsed; break;
                default: throw new ArgumentException($"Unknown RDB benchmark option '{option}'.");
            }
        }

        if (keys < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keys));
        }

        return (keys, repeats);
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }

    private static void PrintTrial(TrialResult result)
    {
        Console.WriteLine($"Trial {result.Trial}: keys={result.Keys:N0}, seed={result.SeedMilliseconds:F2} ms, shutdown+snapshot={result.SnapshotMilliseconds:F2} ms, file={result.FileBytes:N0} bytes, restore-to-ready={result.RestoreMilliseconds:F2} ms, managed-heap-delta={result.ManagedHeapDeltaBytes:N0} bytes");
    }

    private sealed record TrialResult(
        int Trial,
        int Keys,
        double SeedMilliseconds,
        double SnapshotMilliseconds,
        long FileBytes,
        double RestoreMilliseconds,
        long ManagedHeapDeltaBytes);

    private sealed class ServerInstance : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IRedisTcpServer _server;
        private readonly CancellationTokenSource _cancellation = new();
        private Task? _serverTask;
        private bool _stopped;

        public IPEndPoint Endpoint => _server.LocalEndpoint
            ?? throw new InvalidOperationException("CacheVault benchmark server is not listening yet.");

        public ServerInstance(string rdbPath)
        {
            var options = new ServerOptions
            {
                Host = "127.0.0.1",
                Port = 0,
                EnableRdb = true,
                RdbFilePath = rdbPath,
                EnableAof = false,
                MaxMemoryBytes = 0
            };

            var services = new ServiceCollection();
            services.AddCacheVaultServer(options);
            _provider = services.BuildServiceProvider();
            _server = _provider.GetRequiredService<IRedisTcpServer>();
        }

        public async Task StartAsync()
        {
            _serverTask = _server.StartAsync(_cancellation.Token);
            for (int attempt = 0; attempt < 500; attempt++)
            {
                if (_server.LocalEndpoint is not null)
                {
                    return;
                }

                if (_serverTask.IsFaulted || _serverTask.IsCanceled)
                {
                    await _serverTask.ConfigureAwait(false);
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new TimeoutException("CacheVault did not become ready within five seconds.");
        }

        public async Task StopAsync()
        {
            if (_stopped)
            {
                return;
            }

            await _server.StopAsync(CancellationToken.None).ConfigureAwait(false);
            if (_serverTask is not null)
            {
                try
                {
                    await _serverTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _stopped = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_stopped && _serverTask is not null)
                {
                    await StopAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _cancellation.Dispose();
                await _provider.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
