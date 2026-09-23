using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Commands.Implementations;

public sealed class ExecCommand : IRedisCommand {
    private readonly CommandDispatcher _dispatcher;

    public ExecCommand(
        CommandDispatcher dispatcher) {
        ArgumentNullException.ThrowIfNull(
            dispatcher);

        _dispatcher = dispatcher;
    }

    public string Name => "EXEC";

    public async ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        ArgumentNullException.ThrowIfNull(
            context);

        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 0) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'exec' command");
        }

        if (!context.Session.IsInTransaction) {
            throw new CommandArgumentException(
                "ERR EXEC without MULTI");
        }

        IReadOnlyList<QueuedCommand> queuedCommands =
            context.Session.DrainTransaction();

        var responses =
            new List<RespValue>(
                queuedCommands.Count);

        foreach (QueuedCommand queuedCommand in queuedCommands) {
            try {
                RespValue response =
                    await _dispatcher.ExecuteQueuedCommandAsync(
                        context,
                        queuedCommand);

                responses.Add(
                    response);
            }
            catch (CommandArgumentException exception) {
                responses.Add(
                    new RespError(
                        exception.Message));
            }
        }

        return new RespArray(
            responses);
    }
}