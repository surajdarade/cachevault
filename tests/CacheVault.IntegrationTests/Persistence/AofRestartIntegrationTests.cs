using CacheVault.IntegrationTests.Infrastructure;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.IntegrationTests.Persistence;

public sealed class AofRestartIntegrationTests {
    [Fact]
    public async Task ServerRestart_ReplaysAof_AndRestoresDataset() {
        string temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "CacheVault",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            temporaryDirectory);

        string aofFilePath =
            Path.Combine(
                temporaryDirectory,
                "cachevault.aof");

        try {
            await WriteInitialDatasetAsync(
                aofFilePath);

            long aofLengthAfterShutdown =
                new FileInfo(
                    aofFilePath).Length;

            Assert.True(
                aofLengthAfterShutdown > 0);

            long aofLengthAfterRecovery =
                await VerifyDatasetAfterRestartAsync(
                    aofFilePath);

            Assert.Equal(
                aofLengthAfterShutdown,
                aofLengthAfterRecovery);

            long aofLengthAfterNewMutation =
                new FileInfo(
                    aofFilePath).Length;

            Assert.True(
                aofLengthAfterNewMutation >
                aofLengthAfterRecovery);
        }
        finally {
            if (Directory.Exists(
                    temporaryDirectory)) {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    private static async Task WriteInitialDatasetAsync(
        string aofFilePath) {
        var services =
            new ServiceCollection();

        var options =
            new ServerOptions
            {
                Host = "127.0.0.1",
                Port = 0,
                EnableRdb = false,
                EnableAof = true,
                AofFilePath = aofFilePath
            };

        services.AddCacheVaultServer(
            options);

        await using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IRedisTcpServer server =
            serviceProvider.GetRequiredService<
                IRedisTcpServer>();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task serverTask =
            server.StartAsync(
                cancellationTokenSource.Token);

        await WaitForServerToStartAsync(
            server,
            serverTask);

        try {
            await using var client =
                await RedisTestClient.ConnectAsync(
                    server.LocalEndpoint!);

            RespValue setResponse =
                await client.SendCommandAsync(
                    "SET",
                    "name",
                    "CacheVault");

            var setResult =
                Assert.IsType<RespSimpleString>(
                    setResponse);

            Assert.Equal(
                "OK",
                setResult.Value);

            RespValue counterSetResponse =
                await client.SendCommandAsync(
                    "SET",
                    "counter",
                    "10");

            var counterSetResult =
                Assert.IsType<RespSimpleString>(
                    counterSetResponse);

            Assert.Equal(
                "OK",
                counterSetResult.Value);

            RespValue incrementResponse =
                await client.SendCommandAsync(
                    "INCR",
                    "counter");

            var incrementResult =
                Assert.IsType<RespInteger>(
                    incrementResponse);

            Assert.Equal(
                11,
                incrementResult.Value);
        }
        finally {
            await server.StopAsync(
                CancellationToken.None);

            cancellationTokenSource.Cancel();

            try {
                await serverTask;
            }
            catch (OperationCanceledException) {
            }
        }
    }

    private static async Task<long> VerifyDatasetAfterRestartAsync(
        string aofFilePath) {
        var services =
            new ServiceCollection();

        var options =
            new ServerOptions
            {
                Host = "127.0.0.1",
                Port = 0,
                EnableRdb = false,
                EnableAof = true,
                AofFilePath = aofFilePath
            };

        services.AddCacheVaultServer(
            options);

        await using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IRedisTcpServer server =
            serviceProvider.GetRequiredService<
                IRedisTcpServer>();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task serverTask =
            server.StartAsync(
                cancellationTokenSource.Token);

        await WaitForServerToStartAsync(
            server,
            serverTask);

        try {
            long aofLengthAfterRecovery =
                new FileInfo(
                    aofFilePath).Length;

            await using var client =
                await RedisTestClient.ConnectAsync(
                    server.LocalEndpoint!);

            RespValue nameResponse =
                await client.SendCommandAsync(
                    "GET",
                    "name");

            var nameResult =
                Assert.IsType<RespBulkString>(
                    nameResponse);

            Assert.Equal(
                "CacheVault",
                nameResult.Value);

            RespValue counterResponse =
                await client.SendCommandAsync(
                    "GET",
                    "counter");

            var counterResult =
                Assert.IsType<RespBulkString>(
                    counterResponse);

            Assert.Equal(
                "11",
                counterResult.Value);

            RespValue incrementResponse =
                await client.SendCommandAsync(
                    "INCR",
                    "counter");

            var incrementResult =
                Assert.IsType<RespInteger>(
                    incrementResponse);

            Assert.Equal(
                12,
                incrementResult.Value);

            return aofLengthAfterRecovery;
        }
        finally {
            await server.StopAsync(
                CancellationToken.None);

            cancellationTokenSource.Cancel();

            try {
                await serverTask;
            }
            catch (OperationCanceledException) {
            }
        }
    }

    private static async Task WaitForServerToStartAsync(
        IRedisTcpServer server,
        Task serverTask) {
        for (int attempt = 0; attempt < 100; attempt++) {
            if (server.LocalEndpoint is not null) {
                return;
            }

            if (serverTask.IsFaulted) {
                await serverTask;
            }

            if (serverTask.IsCanceled) {
                throw new OperationCanceledException(
                    "CacheVault server startup was canceled.");
            }

            await Task.Delay(10);
        }

        if (serverTask.IsFaulted) {
            await serverTask;
        }

        if (serverTask.IsCanceled) {
            throw new OperationCanceledException(
                "CacheVault server startup was canceled.");
        }

        throw new InvalidOperationException(
            "CacheVault server failed to start.");
    }
}