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

    private IReadOnlyDictionary<string, IRedisCommand> _commands =
        new Dictionary<string, IRedisCommand>(
            StringComparer.OrdinalIgnoreCase);

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

    public ValueTask<RespValue> DispatchAsync(
        CommandContext context,
        RespArray request) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

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

            return ValueTask.FromResult<RespValue>(
                new RespSimpleString("QUEUED"));
        }

        return command.ExecuteAsync(
            context,
            arguments);
    }

    public ValueTask<RespValue> ExecuteQueuedCommandAsync(
        CommandContext context,
        QueuedCommand queuedCommand) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(queuedCommand);

        if (!_commands.TryGetValue(
                queuedCommand.Name,
                out IRedisCommand? command)) {
            throw new CommandArgumentException(
                $"ERR unknown command '{queuedCommand.Name.ToLowerInvariant()}'");
        }

        return command.ExecuteAsync(
            context,
            queuedCommand.Arguments);
    }
}