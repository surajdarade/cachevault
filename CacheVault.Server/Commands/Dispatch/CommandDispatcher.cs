using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Commands.Dispatch;

public sealed class CommandDispatcher {
    private static readonly HashSet<string> TransactionControlCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "MULTI",
            "EXEC",
            "DISCARD",
            "WATCH",
            "UNWATCH"
        };

    private static readonly HashSet<string> PersistentCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "SET",
            "DEL",
            "INCR"
        };

    private IReadOnlyDictionary<string, IRedisCommand> _commands =
        new Dictionary<string, IRedisCommand>(
            StringComparer.OrdinalIgnoreCase);

    private ICommandPersistence? _persistence;

    public void RegisterPersistence(
        ICommandPersistence persistence) {
        ArgumentNullException.ThrowIfNull(
            persistence);

        if (_persistence is not null) {
            throw new InvalidOperationException(
                "Command persistence has already been registered.");
        }

        _persistence = persistence;
    }

    public void RegisterCommands(
        IEnumerable<IRedisCommand> commands) {
        ArgumentNullException.ThrowIfNull(
            commands);

        if (_commands.Count > 0) {
            throw new InvalidOperationException(
                "Commands have already been registered.");
        }

        _commands =
            commands.ToDictionary(
                command => command.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public async ValueTask<RespValue> DispatchAsync(
        CommandContext context,
        RespArray request) {
        ArgumentNullException.ThrowIfNull(
            context);

        ArgumentNullException.ThrowIfNull(
            request);

        if (request.Values is null ||
            request.Values.Count == 0) {
            throw new CommandArgumentException(
                "ERR empty command");
        }

        if (request.Values[0] is not RespBulkString commandName ||
            string.IsNullOrWhiteSpace(commandName.Value)) {
            throw new CommandArgumentException(
                "ERR command must be a bulk string");
        }

        if (!_commands.TryGetValue(
                commandName.Value,
                out IRedisCommand? command)) {
            throw new CommandArgumentException(
                $"ERR unknown command '{commandName.Value.ToLowerInvariant()}'");
        }

        IReadOnlyList<RespValue> arguments =
            request.Values.Count == 1
                ? Array.Empty<RespValue>()
                : request.Values.Skip(1).ToArray();

        if (context.Session.IsInTransaction &&
            !TransactionControlCommands.Contains(command.Name)) {
            context.Session.QueueCommand(
                new QueuedCommand(
                    command.Name,
                    arguments));

            return new RespSimpleString("QUEUED");
        }

        RespValue result =
            await command.ExecuteAsync(
                context,
                arguments);

        if (ShouldPersist(command.Name)) {
            await PersistAsync(
                command.Name,
                arguments,
                context.CancellationToken);
        }

        return result;
    }

    public async ValueTask<RespValue> ExecuteQueuedCommandAsync(
        CommandContext context,
        QueuedCommand queuedCommand) {
        ArgumentNullException.ThrowIfNull(
            context);

        ArgumentNullException.ThrowIfNull(
            queuedCommand);

        if (!_commands.TryGetValue(
                queuedCommand.Name,
                out IRedisCommand? command)) {
            throw new CommandArgumentException(
                $"ERR unknown command '{queuedCommand.Name.ToLowerInvariant()}'");
        }

        RespValue result =
            await command.ExecuteAsync(
                context,
                queuedCommand.Arguments);

        if (ShouldPersist(command.Name)) {
            await PersistAsync(
                command.Name,
                queuedCommand.Arguments,
                context.CancellationToken);
        }

        return result;
    }

    private async ValueTask PersistAsync(
        string commandName,
        IReadOnlyList<RespValue> arguments,
        CancellationToken cancellationToken) {
        if (_persistence is null) {
            return;
        }

        string[] stringArguments =
            ConvertArguments(arguments);

        await _persistence.PersistAsync(
            commandName,
            stringArguments,
            cancellationToken);
    }

    private static bool ShouldPersist(
        string commandName) {
        return PersistentCommands.Contains(
            commandName);
    }

    private static string[] ConvertArguments(
        IReadOnlyList<RespValue> arguments) {
        var result =
            new string[arguments.Count];

        for (int i = 0; i < arguments.Count; i++) {
            if (arguments[i] is not RespBulkString bulkString ||
                bulkString.Value is null) {
                throw new CommandArgumentException(
                    "ERR persistent command arguments must be non-null bulk strings");
            }

            result[i] =
                bulkString.Value;
        }

        return result;
    }
}