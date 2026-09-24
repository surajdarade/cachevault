using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services =
    new();

var serverOptions =
    new ServerOptions();

CommandLineOptions.Apply(
    serverOptions,
    args);

services.AddCacheVaultServer(
    serverOptions);

ServiceProvider serviceProvider =
    services.BuildServiceProvider();

IRedisTcpServer server =
    serviceProvider.GetRequiredService<IRedisTcpServer>();

using CancellationTokenSource cancellationTokenSource =
    new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;

    cancellationTokenSource.Cancel();
};

await server.StartAsync(
    cancellationTokenSource.Token);
