using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
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
            "UNWATCH",
            "SUBSCRIBE",
            "UNSUBSCRIBE"
        };

    private static readonly HashSet<string> PersistentCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "SET",
            "DEL",
            "INCR",
            "LPUSH",
            "RPUSH",
            "LPOP",
            "RPOP"
        };

    private IReadOnlyDictionary<string, IRedisCommand> _commands =
        new Dictionary<string, IRedisCommand>(
            StringComparer.OrdinalIgnoreCase);

    private ICommandPersistence? _persistence;

    private IReplicationManager? _replication;

    private readonly ICommandExecutionGate? _executionGate;
    private readonly bool _isReplica;

    public CommandDispatcher(
        ICommandExecutionGate? executionGate = null,
        bool isReplica = false)
    {
        _executionGate = executionGate;
        _isReplica = isReplica;
    }

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

    public void RegisterReplication(
        IReplicationManager replication) {
        ArgumentNullException.ThrowIfNull(
            replication);

        if (_replication is not null) {
            throw new InvalidOperationException(
                "Command replication has already been registered.");
        }

        _replication = replication;
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
        RespArray request)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        if (_executionGate is null)
        {
            return await DispatchCoreAsync(
                context,
                request);
        }

        await _executionGate.WaitAsync(
            context.CancellationToken);

        try
        {
            return await DispatchCoreAsync(
                context,
                request);
        }
        finally
        {
            _executionGate.Release();
        }
    }

    private async ValueTask<RespValue> DispatchCoreAsync(
        CommandContext context,
        RespArray request)
    {
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

        if (_isReplica &&
            !context.IsReplay &&
            IsWriteCommand(command.Name))
        {
            throw new CommandArgumentException(
                "READONLY You can't write against a read only replica.");
        }

        if (context.Session.IsSubscribed &&
            !IsAllowedInSubscriptionMode(command.Name)) {
            throw new CommandArgumentException(
                "ERR only SUBSCRIBE / UNSUBSCRIBE / PING allowed in this context");
        }

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

        if (!context.IsReplay &&
            ShouldPersist(command.Name)) {
            await PersistAsync(
                command.Name,
                arguments,
                context.CancellationToken);
        }

        if (!context.IsReplay &&
            ShouldReplicate(command.Name)) {
            await ReplicateAsync(
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
                out IRedisCommand? command))
        {
            throw new CommandArgumentException(
                $"ERR unknown command '{queuedCommand.Name.ToLowerInvariant()}'");
        }

        if (_isReplica &&
            !context.IsReplay &&
            IsWriteCommand(command.Name))
        {
            throw new CommandArgumentException(
                "READONLY You can't write against a read only replica.");
        }

        RespValue result =
            await command.ExecuteAsync(
                context,
                queuedCommand.Arguments);

        if (!context.IsReplay &&
            ShouldPersist(command.Name)) {
            await PersistAsync(
                command.Name,
                queuedCommand.Arguments,
                context.CancellationToken);
        }

        if (!context.IsReplay &&
            ShouldReplicate(command.Name)) {
            await ReplicateAsync(
                command.Name,
                queuedCommand.Arguments,
                context.CancellationToken);
        }

        return result;
    }

    private static bool IsWriteCommand(
        string commandName)
    {
        return commandName.Equals("SET", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("DEL", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("INCR", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("LPUSH", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("RPUSH", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("LPOP", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("RPOP", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedInSubscriptionMode(
        string commandName) {
        return commandName.Equals(
                   "SUBSCRIBE",
                   StringComparison.OrdinalIgnoreCase)
               ||
               commandName.Equals(
                   "UNSUBSCRIBE",
                   StringComparison.OrdinalIgnoreCase)
               ||
               commandName.Equals(
                   "PING",
                   StringComparison.OrdinalIgnoreCase);
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

    private async ValueTask ReplicateAsync(
        string commandName,
        IReadOnlyList<RespValue> arguments,
        CancellationToken cancellationToken) {
        if (_replication is null) {
            return;
        }

        string[] stringArguments =
            ConvertArguments(arguments);

        await _replication.ReplicateAsync(
            commandName,
            stringArguments,
            cancellationToken);
    }

    private static bool ShouldPersist(
        string commandName) {
        return PersistentCommands.Contains(
            commandName);
    }

    private static bool ShouldReplicate(
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