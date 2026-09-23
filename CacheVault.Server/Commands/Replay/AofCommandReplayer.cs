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
        ArgumentNullException.ThrowIfNull(
            reader);

        ArgumentNullException.ThrowIfNull(
            dispatcher);

        _reader = reader;
        _dispatcher = dispatcher;
    }

    public ValueTask<int> ReplayAsync(
        Stream stream,
        CancellationToken cancellationToken = default) {
        return ReplayAsync(
            stream,
            0,
            cancellationToken);
    }

    public async ValueTask<int> ReplayAsync(
        Stream stream,
        long startOffset,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (!stream.CanRead) {
            throw new ArgumentException(
                "The stream must be readable.",
                nameof(stream));
        }

        if (!stream.CanSeek) {
            throw new ArgumentException(
                "The stream must support seeking.",
                nameof(stream));
        }

        if (startOffset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(startOffset),
                "AOF start offset cannot be negative.");
        }

        if (startOffset > stream.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(startOffset),
                "AOF start offset cannot be greater than the stream length.");
        }

        stream.Position = startOffset;

        var session =
            new ClientSession();

        var context =
            new CommandContext(
                session,
                cancellationToken,
                isReplay: true);

        int replayedCommands = 0;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            RespArray? command =
                _reader.ReadCommand(
                    stream);

            if (command is null) {
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