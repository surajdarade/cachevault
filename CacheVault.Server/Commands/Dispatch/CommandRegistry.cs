using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Replication.Abstractions;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;
using CacheVault.Server.PubSub;
using CacheVault.Server.Configuration;
using CacheVault.Server.Replication;

namespace CacheVault.Server.Commands.Dispatch;

public static class CommandRegistry {
    public static IReadOnlyList<IRedisCommand>
        CreateDefaultCommands(
            IKeyValueStore store,
            IClock clock,
            CommandDispatcher dispatcher,
            IReplicationWaiter replicationWaiter,
            PubSubManager? pubSubManager = null,
            IListStore? listStore = null,
            IReplicationState? replicationState = null,
            IReplicaRegistry? replicaRegistry = null,
            ServerOptions? serverOptions = null,
            ReplicaSynchronizationStatus? replicaStatus = null) {
        ArgumentNullException.ThrowIfNull(
            store);

        ArgumentNullException.ThrowIfNull(
            clock);

        ArgumentNullException.ThrowIfNull(
            dispatcher);

        ArgumentNullException.ThrowIfNull(
            replicationWaiter);

        pubSubManager ??= new PubSubManager();
        listStore ??= new InMemoryListStore();

        replicationState ??=
            new CacheVault.Replication.State.ReplicationState();

        replicaRegistry ??=
            new CacheVault.Replication.Master.ReplicaRegistry();

        serverOptions ??=
            new ServerOptions();

        replicaStatus ??=
            new ReplicaSynchronizationStatus();

        return
        [
            new PingCommand(),
            new EchoCommand(),
            new SetCommand(store, clock, listStore),
            new GetCommand(store, listStore),
            new DelCommand(store, listStore),
            new IncrCommand(store, listStore),
            new TtlCommand(store, clock),
            new PttlCommand(store, clock),
            new MultiCommand(),
            new ExecCommand(dispatcher, store, listStore),
            new WatchCommand(store, listStore),
            new UnwatchCommand(),
            new DiscardCommand(),
            new WaitCommand(replicationWaiter),
            new InfoCommand(
                replicationState,
                replicaRegistry,
                serverOptions,
                replicaStatus),
            new RoleCommand(
                replicationState,
                replicaRegistry,
                serverOptions,
                replicaStatus),
            new SubscribeCommand(pubSubManager),
            new UnsubscribeCommand(pubSubManager),
            new PublishCommand(pubSubManager),
            new LpushCommand(listStore, store),
            new RpushCommand(listStore, store),
            new LpopCommand(listStore, store),
            new RpopCommand(listStore, store),
            new LrangeCommand(listStore, store),
            new LlenCommand(listStore, store)
        ];
    }
}