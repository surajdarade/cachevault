using CacheVault.Core.Abstractions;
using CacheVault.Core.Eviction;
using CacheVault.Core.Eviction.Abstractions;
using CacheVault.Core.Storage;
using CacheVault.Core.Lists;
using CacheVault.Infrastructure.Time;
using CacheVault.Persistence.Aof.Reading;
using CacheVault.Persistence.Aof.Writing;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Persistence.Rdb.Writing;
using CacheVault.Persistence.Recovery;
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
using CacheVault.Server.Replication;
using CacheVault.Server.PubSub;
using Microsoft.Extensions.DependencyInjection;

namespace CacheVault.Server.Configuration;

public static class ServerServiceCollectionExtensions {
    private const long ReplicationBacklogCapacity =
        64L * 1024L * 1024L;

    public static IServiceCollection AddCacheVaultServer(
        this IServiceCollection services,
        ServerOptions options) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);

        services.AddSingleton<PubSubManager>();

        services.AddSingleton<
            InMemoryListStore>(
            serviceProvider =>
            {
                IEvictionManager evictionManager =
                    serviceProvider.GetRequiredService<
                        IEvictionManager>();

                IEvictionCoordinator evictionCoordinator =
                    serviceProvider.GetRequiredService<
                        IEvictionCoordinator>();

                return new InMemoryListStore(
                    evictionManager,
                    evictionCoordinator);
            });

        services.AddSingleton<IListStore>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    InMemoryListStore>());

        services.AddSingleton<IListStoreSnapshot>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    InMemoryListStore>());

        services.AddSingleton<IResettableListStore>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    InMemoryListStore>());

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
            IEvictionManager>(
            new EvictionManager(
                options.MaxMemoryBytes,
                options.EvictionPolicy));

        services.AddSingleton<
            IEvictionCoordinator,
            EvictionCoordinator>();

        services.AddSingleton<
            InMemoryKeyValueStore>(
            serviceProvider =>
            {
                IClock clock =
                    serviceProvider.GetRequiredService<
                        IClock>();

                IEvictionManager evictionManager =
                    serviceProvider.GetRequiredService<
                        IEvictionManager>();

                IEvictionCoordinator evictionCoordinator =
                    serviceProvider.GetRequiredService<
                        IEvictionCoordinator>();

                return new InMemoryKeyValueStore(
                    clock,
                    evictionManager,
                    evictionCoordinator);
            });

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

        // -----------------------------------------------------------------
        // Replication state
        // -----------------------------------------------------------------

        services.AddSingleton<
            IReplicationState>(
            new ReplicationState(
                isMaster: !options.IsReplica));

        services.AddSingleton<
            ReplicaSynchronizationStatus>();

        services.AddSingleton<
            IReplicationBacklog>(
            new ReplicationBacklog(
                ReplicationBacklogCapacity));

        services.AddSingleton<
            IReplicaRegistry,
            ReplicaRegistry>();

        services.AddSingleton<
            IReplicaConnectionRegistry,
            ReplicaConnectionRegistry>();

        // -----------------------------------------------------------------
        // Replication command encoding / protocol
        // -----------------------------------------------------------------

        services.AddSingleton<
            IReplicationCommandEncoder,
            RespReplicationCommandEncoder>();

        services.AddSingleton<
            IReplicationProtocolEncoder,
            RespReplicationProtocolEncoder>();

        services.AddSingleton<
            IReplicationProtocolParser,
            RespReplicationProtocolParser>();

        // -----------------------------------------------------------------
        // Replication propagation
        // -----------------------------------------------------------------

        services.AddSingleton<
            IReplicationBroadcaster,
            ReplicationBroadcaster>();

        services.AddSingleton<
            IReplicationManager,
            ReplicationManager>();

        // -----------------------------------------------------------------
        // RDB snapshot provider used by FULLRESYNC
        // -----------------------------------------------------------------

        services.AddSingleton<
            IReplicationSnapshotProvider,
            RdbReplicationSnapshotProvider>();

        // -----------------------------------------------------------------
        // WAIT
        // -----------------------------------------------------------------

        services.AddSingleton<
            IReplicationWaiter,
            ReplicationWaiter>();

        // -----------------------------------------------------------------
        // AOF persistence
        // -----------------------------------------------------------------

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

        // -----------------------------------------------------------------
        // Command execution gate
        // -----------------------------------------------------------------

        services.AddSingleton<
            ICommandExecutionGate,
            CommandExecutionGate>();

        // -----------------------------------------------------------------
        // Command dispatcher
        // -----------------------------------------------------------------

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

                IReplicationWaiter replicationWaiter =
                    serviceProvider.GetRequiredService<
                        IReplicationWaiter>();

                IReplicationState replicationState =
                    serviceProvider.GetRequiredService<
                        IReplicationState>();

                PubSubManager pubSubManager =
                    serviceProvider.GetRequiredService<
                        PubSubManager>();

                IListStore listStore =
                    serviceProvider.GetRequiredService<
                        IListStore>();

                ICommandExecutionGate executionGate =
                    serviceProvider.GetRequiredService<
                        ICommandExecutionGate>();

                var dispatcher =
                    new CommandDispatcher(
                        executionGate,
                        options.IsReplica);

                IReadOnlyList<IRedisCommand> commands =
                    CommandRegistry.CreateDefaultCommands(
                        store,
                        clock,
                        dispatcher,
                        replicationWaiter,
                        pubSubManager,
                        listStore,
                        replicationState,
                        serviceProvider.GetRequiredService<IReplicaRegistry>(),
                        options,
                        serviceProvider.GetRequiredService<ReplicaSynchronizationStatus>());

                dispatcher.RegisterCommands(
                    commands);

                dispatcher.RegisterPersistence(
                    persistence);

                dispatcher.RegisterReplication(
                    replication);

                return dispatcher;
            });

        // -----------------------------------------------------------------
        // RDB persistence / recovery
        // -----------------------------------------------------------------

        services.AddSingleton<
            RdbSnapshotReader>();

        services.AddSingleton(
            serviceProvider =>
            {
                IKeyValueStoreSnapshot snapshotStore =
                    serviceProvider.GetRequiredService<
                        IKeyValueStoreSnapshot>();

                IListStoreSnapshot listSnapshotStore =
                    serviceProvider.GetRequiredService<
                        IListStoreSnapshot>();

                return new RdbSnapshotWriter(
                    snapshotStore,
                    listSnapshotStore);
            });

        services.AddSingleton<
            RdbPersistenceService>();

        services.AddSingleton(
            serviceProvider =>
            {
                IKeyValueStore store =
                    serviceProvider.GetRequiredService<
                        IKeyValueStore>();

                RdbSnapshotReader reader =
                    serviceProvider.GetRequiredService<
                        RdbSnapshotReader>();

                IListStore listStore =
                    serviceProvider.GetRequiredService<
                        IListStore>();

                return new RdbSnapshotLoader(
                    store,
                    reader,
                    listStore);
            });

        // -----------------------------------------------------------------
        // AOF recovery
        // -----------------------------------------------------------------

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

        // -----------------------------------------------------------------
        // Networking
        // -----------------------------------------------------------------

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

        // -----------------------------------------------------------------
        // Replica-side synchronization
        // -----------------------------------------------------------------

        services.AddSingleton<
            ReplicaSynchronizationService>();

        return services;
    }
}