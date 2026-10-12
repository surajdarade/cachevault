using CacheVault.IntegrationTests.Infrastructure;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.IntegrationTests.Persistence;

public sealed class RdbRestartIntegrationTests {
    [Fact]
    public async Task ServerRestart_LoadsRdb_AndRestoresStringsAndLists() {
        string temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "CacheVault",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            temporaryDirectory);

        string rdbFilePath =
            Path.Combine(
                temporaryDirectory,
                "cachevault.rdb");

        try {
            await WriteInitialDatasetAsync(
                rdbFilePath);

            Assert.True(
                File.Exists(rdbFilePath),
                "The RDB snapshot should be created when the server stops.");

            Assert.True(
                new FileInfo(rdbFilePath).Length > 0,
                "The RDB snapshot should not be empty.");

            await VerifyDatasetAfterRestartAsync(
                rdbFilePath);
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
        string rdbFilePath) {
        var services =
            new ServiceCollection();

        var options =
            new ServerOptions {
                Host = "127.0.0.1",
                Port = 0,
                EnableRdb = true,
                RdbFilePath = rdbFilePath,
                EnableAof = false
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

            Assert.Equal(
                new RespSimpleString("OK"),
                setResponse);

            RespValue listResponse =
                await client.SendCommandAsync(
                    "RPUSH",
                    "items",
                    "first",
                    "second");

            Assert.Equal(
                new RespInteger(2),
                listResponse);
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

    private static async Task VerifyDatasetAfterRestartAsync(
        string rdbFilePath) {
        var services =
            new ServiceCollection();

        var options =
            new ServerOptions {
                Host = "127.0.0.1",
                Port = 0,
                EnableRdb = true,
                RdbFilePath = rdbFilePath,
                EnableAof = false
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

            RespValue nameResponse =
                await client.SendCommandAsync(
                    "GET",
                    "name");

            Assert.Equal(
                new RespBulkString("CacheVault"),
                nameResponse);

            RespValue listResponse =
                await client.SendCommandAsync(
                    "LRANGE",
                    "items",
                    "0",
                    "-1");

            var listResult =
                Assert.IsType<RespArray>(
                    listResponse);

            Assert.NotNull(
                listResult.Values);

            Assert.Equal(
                new RespValue[] {
                    new RespBulkString("first"),
                    new RespBulkString("second")
                },
                listResult.Values);
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
