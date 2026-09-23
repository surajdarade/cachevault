using CacheVault.IntegrationTests.Infrastructure;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.IntegrationTests.Persistence;

public sealed class AofExpirationRestartIntegrationTests {
    [Fact]
    public async Task ServerRestart_PreservesAbsoluteAofExpiration() {
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
            await WriteExpiringValueAsync(
                aofFilePath);

            await Task.Delay(
                TimeSpan.FromSeconds(1));

            await VerifyExpirationAfterRestartAsync(
                aofFilePath);
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

    private static async Task WriteExpiringValueAsync(
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

            RespValue response =
                await client.SendCommandAsync(
                    "SET",
                    "expiring-key",
                    "value",
                    "PX",
                    "10000");

            var result =
                Assert.IsType<RespSimpleString>(
                    response);

            Assert.Equal(
                "OK",
                result.Value);
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

    private static async Task VerifyExpirationAfterRestartAsync(
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

            RespValue ttlResponse =
                await client.SendCommandAsync(
                    "PTTL",
                    "expiring-key");

            var ttlResult =
                Assert.IsType<RespInteger>(
                    ttlResponse);

            Assert.InRange(
                ttlResult.Value,
                1,
                9000);

            RespValue getResponse =
                await client.SendCommandAsync(
                    "GET",
                    "expiring-key");

            var getResult =
                Assert.IsType<RespBulkString>(
                    getResponse);

            Assert.Equal(
                "value",
                getResult.Value);
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