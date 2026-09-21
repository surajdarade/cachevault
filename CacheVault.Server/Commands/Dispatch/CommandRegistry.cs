using CacheVault.Core.Abstractions;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Implementations;

namespace CacheVault.Server.Commands.Dispatch;

public static class CommandRegistry {
    public static IReadOnlyList<IRedisCommand> CreateDefaultCommands(
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(store);

        return
        [
            new PingCommand(),
            new EchoCommand(),
            new SetCommand(store),
            new GetCommand(store),
            new DelCommand(store),
            new IncrCommand(store)
        ];
    }
}