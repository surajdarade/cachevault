using CacheVault.Persistence.Aof.Reading;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.Commands.Replay;

public sealed class AofCommandReplayer {
    private readonly AofCommandReader _reader;
    private readonly CommandDispatcher _dispatcher;

    public AofCommandReplayer(
        AofCommandReader reader,
        CommandDispatcher dispatcher) {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _reader = reader;
        _dispatcher = dispatcher;
    }

    public async ValueTask<int> ReplayAsync(
        Stream stream,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead) {
            throw new ArgumentException(
                "The stream must be readable.",
                nameof(stream));
        }

        var session = new ClientSession();

        var context =
            new CommandContext(
                session,
                cancellationToken);

        int replayedCommands = 0;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            RespArray command;

            try {
                command = _reader.ReadCommand(stream);
            }
            catch (EndOfStreamException) {
                return replayedCommands;
            }

            await _dispatcher
                .DispatchAsync(
                    context,
                    command)
                .ConfigureAwait(false);

            replayedCommands++;
        }
    }
}