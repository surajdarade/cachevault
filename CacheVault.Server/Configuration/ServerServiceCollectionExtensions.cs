using CacheVault.Core.Abstractions;
using CacheVault.Core.Storage;
using CacheVault.Infrastructure.Time;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Abstractions;
using CacheVault.Server.Networking.Connections;
using CacheVault.Server.Networking.Server;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.Server.Configuration;

public static class ServerServiceCollectionExtensions {
    public static IServiceCollection AddCacheVaultServer(
        this IServiceCollection services,
        ServerOptions options) {
        ArgumentNullException.ThrowIfNull(
            services);

        ArgumentNullException.ThrowIfNull(
            options);

        services.AddSingleton(
            options);

        services.AddSingleton<
            IClock,
            SystemClock>();

        services.AddSingleton<
            IRespParser,
            RespParser>();

        services.AddSingleton<
            IRespSerializer,
            RespSerializer>();

        services.AddSingleton<
            IRespStreamReaderFactory,
            RespStreamReaderFactory>();

        services.AddSingleton<
            IKeyValueStore,
            InMemoryKeyValueStore>();

        services.AddSingleton(
            serviceProvider =>
            {
                IKeyValueStore store =
                    serviceProvider.GetRequiredService<IKeyValueStore>();

                IClock clock =
                    serviceProvider.GetRequiredService<IClock>();

                var dispatcher =
                    new CommandDispatcher();

                IReadOnlyList<IRedisCommand> commands =
                    CommandRegistry.CreateDefaultCommands(
                        store,
                        clock,
                        dispatcher);

                dispatcher.RegisterCommands(
                    commands);

                return dispatcher;
            });

        services.AddSingleton<
            IClientConnectionHandler,
            ClientConnectionHandler>();

        services.AddSingleton<
            IRedisTcpServer,
            RedisTcpServer>();

        return services;
    }
}