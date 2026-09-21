using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Dispatch;

public sealed class CommandDispatcher {
    private readonly IReadOnlyDictionary<string, IRedisCommand> _commands;

    public CommandDispatcher(
        IEnumerable<IRedisCommand> commands) {
        _commands = commands.ToDictionary(
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

        return command.ExecuteAsync(
            context,
            arguments);
    }
}