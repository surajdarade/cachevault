using CacheVault.Core.Abstractions;
using CacheVault.Core.Storage;
using CacheVault.Infrastructure.Time;
using CacheVault.Persistence.Aof.Reading;
using CacheVault.Persistence.Aof.Writing;
using CacheVault.Persistence.Recovery;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Commands.Persistence;
using CacheVault.Server.Commands.Replay;
using CacheVault.Server.Networking.Abstractions;
using CacheVault.Server.Networking.Connections;
using CacheVault.Server.Networking.Server;
using CacheVault.Server.Recovery;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.Server.Configuration;

public static class ServerServiceCollectionExtensions {
    private const long ReplicationBacklogCapacity =
        64L * 1024L * 1024L;

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
            InMemoryKeyValueStore>();

        services.AddSingleton<
            IKeyValueStore>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    InMemoryKeyValueStore>());

        services.AddSingleton<
            IKeyValueStoreSnapshot>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    InMemoryKeyValueStore>());

        services.AddSingleton<
            IReplicationState,
            ReplicationState>();

        services.AddSingleton<
            IReplicationCommandEncoder,
            RespReplicationCommandEncoder>();

        services.AddSingleton<
            IReplicationBacklog>(
            new ReplicationBacklog(
                ReplicationBacklogCapacity));

        services.AddSingleton<
            IReplicaConnectionRegistry,
            ReplicaConnectionRegistry>();

        services.AddSingleton<
            IReplicationBroadcaster,
            ReplicationBroadcaster>();

        services.AddSingleton<
            IReplicationManager,
            ReplicationManager>();

        services.AddSingleton<
            IReplicationProtocolParser,
            RespReplicationProtocolParser>();

        services.AddSingleton<
            IReplicationSnapshotProvider,
            RdbReplicationSnapshotProvider>();

        if (options.EnableAof) {
            services.AddSingleton<
                AofWriter>(
                serviceProvider =>
                {
                    IRespSerializer serializer =
                        serviceProvider.GetRequiredService<
                            IRespSerializer>();

                    return new AofWriter(
                        options.AofFilePath,
                        serializer);
                });

            services.AddSingleton<
                ICommandPersistence>(
                serviceProvider =>
                {
                    AofWriter writer =
                        serviceProvider.GetRequiredService<
                            AofWriter>();

                    IKeyValueStore store =
                        serviceProvider.GetRequiredService<
                            IKeyValueStore>();

                    return new AofCommandPersistence(
                        writer,
                        store);
                });
        }
        else {
            services.AddSingleton<
                ICommandPersistence,
                NoOpCommandPersistence>();
        }

        services.AddSingleton(
            serviceProvider =>
            {
                IKeyValueStore store =
                    serviceProvider.GetRequiredService<
                        IKeyValueStore>();

                IClock clock =
                    serviceProvider.GetRequiredService<
                        IClock>();

                ICommandPersistence persistence =
                    serviceProvider.GetRequiredService<
                        ICommandPersistence>();

                IReplicationManager replication =
                    serviceProvider.GetRequiredService<
                        IReplicationManager>();

                var dispatcher =
                    new CommandDispatcher();

                IReadOnlyList<IRedisCommand> commands =
                    CommandRegistry.CreateDefaultCommands(
                        store,
                        clock,
                        dispatcher);

                dispatcher.RegisterCommands(
                    commands);

                dispatcher.RegisterPersistence(
                    persistence);

                dispatcher.RegisterReplication(
                    replication);

                return dispatcher;
            });

        services.AddSingleton<
            RdbSnapshotReader>();

        services.AddSingleton(
            serviceProvider =>
            {
                IKeyValueStore store =
                    serviceProvider.GetRequiredService<
                        IKeyValueStore>();

                RdbSnapshotReader reader =
                    serviceProvider.GetRequiredService<
                        RdbSnapshotReader>();

                return new RdbSnapshotLoader(
                    store,
                    reader);
            });

        services.AddSingleton<
            AofCommandReader>();

        services.AddSingleton(
            serviceProvider =>
            {
                AofCommandReader reader =
                    serviceProvider.GetRequiredService<
                        AofCommandReader>();

                CommandDispatcher dispatcher =
                    serviceProvider.GetRequiredService<
                        CommandDispatcher>();

                return new AofCommandReplayer(
                    reader,
                    dispatcher);
            });

        services.AddSingleton<
            RecoveryCoordinator>();

        services.AddSingleton<
            IClientConnectionHandler,
            ClientConnectionHandler>();

        services.AddSingleton<
            ReplicaConnectionHandler>();

        services.AddSingleton<
            IConnectionHandler,
            ConnectionHandler>();

        services.AddSingleton<
            IRedisTcpServer,
            RedisTcpServer>();

        return services;
    }
}