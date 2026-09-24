using CacheVault.Core.Abstractions;
using CacheVault.Replication.Abstractions;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;

namespace CacheVault.Server.Commands.Dispatch;

public static class CommandRegistry {
    public static IReadOnlyList<IRedisCommand>
        CreateDefaultCommands(
            IKeyValueStore store,
            IClock clock,
            CommandDispatcher dispatcher,
            IReplicationWaiter replicationWaiter) {
        ArgumentNullException.ThrowIfNull(
            store);

        ArgumentNullException.ThrowIfNull(
            clock);

        ArgumentNullException.ThrowIfNull(
            dispatcher);

        ArgumentNullException.ThrowIfNull(
            replicationWaiter);

        return
        [
            new PingCommand(),
            new EchoCommand(),
            new SetCommand(store, clock),
            new GetCommand(store),
            new DelCommand(store),
            new IncrCommand(store),
            new TtlCommand(store, clock),
            new PttlCommand(store, clock),
            new MultiCommand(),
            new ExecCommand(dispatcher, store),
            new WatchCommand(store),
            new UnwatchCommand(),
            new DiscardCommand(),
            new WaitCommand(replicationWaiter)
        ];
    }
}