using CacheVault.Persistence.Recovery;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Protocol;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Configuration;
using CacheVault.Server.Networking.Connections;
using System.Globalization;
using System.Net.Sockets;

namespace CacheVault.Server.Replication;

public sealed class ReplicaSynchronizationService {
    private readonly ServerOptions _options;

    private readonly IReplicationProtocolEncoder
        _protocolEncoder;

    private readonly IReplicationProtocolParser
        _protocolParser;

    private readonly IRespParser _respParser;

    private readonly IRespSerializer _respSerializer;

    private readonly RdbSnapshotLoader _snapshotLoader;

    private readonly CommandDispatcher _commandDispatcher;

    private string _replicationId = "?";

    private long _replicationOffset = -1;

    public ReplicaSynchronizationService(
        ServerOptions options,
        IReplicationProtocolEncoder protocolEncoder,
        IReplicationProtocolParser protocolParser,
        IRespParser respParser,
        IRespSerializer respSerializer,
        RdbSnapshotLoader snapshotLoader,
        CommandDispatcher commandDispatcher) {
        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            protocolEncoder);

        ArgumentNullException.ThrowIfNull(
            protocolParser);

        ArgumentNullException.ThrowIfNull(
            respParser);

        ArgumentNullException.ThrowIfNull(
            respSerializer);

        ArgumentNullException.ThrowIfNull(
            snapshotLoader);

        ArgumentNullException.ThrowIfNull(
            commandDispatcher);

        _options =
            options;

        _protocolEncoder =
            protocolEncoder;

        _protocolParser =
            protocolParser;

        _respParser =
            respParser;

        _respSerializer =
            respSerializer;

        _snapshotLoader =
            snapshotLoader;

        _commandDispatcher =
            commandDispatcher;
    }

    public long ReplicationOffset =>
        _replicationOffset;

    public string ReplicationId =>
        _replicationId;

    public async Task SynchronizeAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            _options.MasterHost,
            _options.MasterPort,
            cancellationToken);

        await using NetworkStream stream =
            client.GetStream();

        var reader =
            new ReplicationStreamReader(
                _respParser);

        await SendListeningPortAsync(
            stream,
            reader,
            cancellationToken);

        await SendPsync2CapabilityAsync(
            stream,
            reader,
            cancellationToken);

        await SendPsyncAsync(
            stream,
            reader,
            cancellationToken);

        await ConsumeReplicationStreamAsync(
            stream,
            reader,
            cancellationToken);
    }

    private async Task SendListeningPortAsync(
        NetworkStream stream,
        ReplicationStreamReader reader,
        CancellationToken cancellationToken) {
        var command =
            new ReplConfCommand(
                "listening-port",
                [
                    _options.Port.ToString(
                        CultureInfo.InvariantCulture)
                ]);

        await stream.WriteAsync(
            _protocolEncoder.EncodeReplConf(
                command),
            cancellationToken);

        RespValue response =
            await ReadRequiredResponseAsync(
                stream,
                reader,
                cancellationToken);

        EnsureOkResponse(
            response);
    }

    private async Task SendPsync2CapabilityAsync(
        NetworkStream stream,
        ReplicationStreamReader reader,
        CancellationToken cancellationToken) {
        var command =
            new ReplConfCommand(
                "capa",
                ["psync2"]);

        await stream.WriteAsync(
            _protocolEncoder.EncodeReplConf(
                command),
            cancellationToken);

        RespValue response =
            await ReadRequiredResponseAsync(
                stream,
                reader,
                cancellationToken);

        EnsureOkResponse(
            response);
    }

    private async Task SendPsyncAsync(
        NetworkStream stream,
        ReplicationStreamReader reader,
        CancellationToken cancellationToken) {
        var command =
            new PsyncCommand(
                _replicationId,
                _replicationOffset);

        await stream.WriteAsync(
            _protocolEncoder.EncodePsync(
                command),
            cancellationToken);

        RespValue response =
            await ReadRequiredResponseAsync(
                stream,
                reader,
                cancellationToken);

        if (_protocolParser.IsContinue(
                response)) {
            return;
        }

        FullResyncResponse fullResync =
            _protocolParser.ParseFullResync(
                response);

        _replicationId =
            fullResync.ReplicationId;

        _replicationOffset =
            fullResync.ReplicationOffset;

        byte[] snapshot =
            await reader.ReadBulkPayloadAsync(
                stream,
                cancellationToken);

        using var snapshotStream =
            new MemoryStream(
                snapshot,
                writable: false);

        _snapshotLoader.LoadWithMetadata(
            snapshotStream,
            DateTimeOffset.UtcNow);
    }

    private async Task ConsumeReplicationStreamAsync(
        NetworkStream stream,
        ReplicationStreamReader reader,
        CancellationToken cancellationToken) {
        var session =
            new ClientSession();

        var context =
            new CommandContext(
                session,
                cancellationToken,
                isReplay: true);

        while (!cancellationToken.IsCancellationRequested) {
            RespValue? message =
                await reader.ReadRespAsync(
                    stream,
                    cancellationToken);

            if (message is null) {
                return;
            }

            if (message is not RespArray command) {
                throw new InvalidDataException(
                    "Replication stream contained a non-command RESP value.");
            }

            if (IsGetAcknowledgement(command)) {
                await SendAcknowledgementAsync(
                    stream,
                    cancellationToken);

                continue;
            }

            try {
                await _commandDispatcher.DispatchAsync(
                    context,
                    command);
            }
            catch (CommandArgumentException exception) {
                throw new InvalidDataException(
                    "Replica could not apply a replicated command.",
                    exception);
            }

            _replicationOffset =
                checked(
                    _replicationOffset +
                    _respSerializer.Serialize(
                        command).LongLength);
        }
    }

    private bool IsGetAcknowledgement(
        RespArray command) {
        if (command.Values is null ||
            command.Values.Count != 3) {
            return false;
        }

        if (command.Values[0] is not RespBulkString commandName ||
            commandName.Value is null ||
            !commandName.Value.Equals(
                "REPLCONF",
                StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (command.Values[1] is not RespBulkString subCommand ||
            subCommand.Value is null ||
            !subCommand.Value.Equals(
                "GETACK",
                StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (command.Values[2] is not RespBulkString argument ||
            argument.Value is null) {
            return false;
        }

        return argument.Value == "*";
    }

    private async Task SendAcknowledgementAsync(
        NetworkStream stream,
        CancellationToken cancellationToken) {
        if (_replicationOffset < 0) {
            throw new InvalidOperationException(
                "Replica replication offset has not been initialized.");
        }

        ReplicationAck acknowledgement =
            new(_replicationOffset);

        await stream.WriteAsync(
            _protocolEncoder.EncodeAck(
                acknowledgement),
            cancellationToken);
    }

    private static async Task<RespValue>
        ReadRequiredResponseAsync(
            NetworkStream stream,
            ReplicationStreamReader reader,
            CancellationToken cancellationToken) {
        RespValue? response =
            await reader.ReadRespAsync(
                stream,
                cancellationToken);

        if (response is null) {
            throw new EndOfStreamException(
                "Master closed the replication connection.");
        }

        return response;
    }

    private static void EnsureOkResponse(
        RespValue response) {
        if (response is RespError error) {
            throw new InvalidOperationException(
                $"Master rejected replication handshake: {error.Message}");
        }

        if (response is not RespSimpleString simpleString ||
            !simpleString.Value.Equals(
                "OK",
                StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                "Master returned an unexpected replication handshake response.");
        }
    }
}