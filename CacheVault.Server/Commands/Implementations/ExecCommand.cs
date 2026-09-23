using CacheVault.Core.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Commands.Implementations;

public sealed class ExecCommand : IRedisCommand {
    private readonly CommandDispatcher _dispatcher;
    private readonly IKeyValueStore _store;

    public ExecCommand(
        CommandDispatcher dispatcher,
        IKeyValueStore store) {
        ArgumentNullException.ThrowIfNull(
            dispatcher);

        ArgumentNullException.ThrowIfNull(
            store);

        _dispatcher =
            dispatcher;

        _store =
            store;
    }

    public string Name =>
        "EXEC";

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

        if (context.Session.WatchState.HasChanged(
                _store.GetVersion)) {
            context.Session.DrainTransaction();
            context.Session.WatchState.Clear();

            return new RespArray(
                null);
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

        context.Session.WatchState.Clear();

        return new RespArray(
            responses);
    }
}