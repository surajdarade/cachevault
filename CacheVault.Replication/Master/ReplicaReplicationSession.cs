using System.Text;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicaReplicationSession {
    private readonly ReplicaConnection _connection;
    private readonly ReplicationHandshake _handshake;
    private readonly FullResynchronizationCoordinator
        _fullResynchronizationCoordinator;
    private readonly PartialResynchronizationCoordinator
        _partialResynchronizationCoordinator;
    private readonly IReplicationProtocolParser _parser;

    public ReplicaReplicationSession(
        ReplicaConnection connection,
        ReplicationHandshake handshake,
        FullResynchronizationCoordinator
            fullResynchronizationCoordinator,
        PartialResynchronizationCoordinator
            partialResynchronizationCoordinator,
        IReplicationProtocolParser parser) {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            handshake);

        ArgumentNullException.ThrowIfNull(
            fullResynchronizationCoordinator);

        ArgumentNullException.ThrowIfNull(
            partialResynchronizationCoordinator);

        ArgumentNullException.ThrowIfNull(
            parser);

        _connection =
            connection;

        _handshake =
            handshake;

        _fullResynchronizationCoordinator =
            fullResynchronizationCoordinator;

        _partialResynchronizationCoordinator =
            partialResynchronizationCoordinator;

        _parser =
            parser;
    }

    public ReplicationHandshakeState State =>
        _handshake.State;

    public void Start() {
        _handshake.Start();
    }

    public async ValueTask ProcessAsync(
        RespValue message,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            message);

        cancellationToken.ThrowIfCancellationRequested();

        if (message is not RespArray array ||
            array.Values is null ||
            array.Values.Count == 0 ||
            array.Values[0] is not RespBulkString command ||
            command.Value is null) {
            throw new FormatException(
                "Replication command must be a non-empty RESP array.");
        }

        switch (command.Value.ToUpperInvariant()) {
            case "REPLCONF":
                await ProcessReplConfAsync(
                    message,
                    cancellationToken);
                return;

            case "PSYNC":
                await ProcessPsyncAsync(
                    message,
                    cancellationToken);
                return;

            default:
                throw new FormatException(
                    $"Unsupported replication command '{command.Value}'.");
        }
    }

    private async ValueTask ProcessReplConfAsync(
        RespValue message,
        CancellationToken cancellationToken) {
        ReplConfCommand command =
            _parser.ParseReplConf(
                message);

        _handshake.ProcessReplConf(
            command);

        await _connection.WriteAsync(
            Encoding.UTF8.GetBytes(
                "+OK\r\n"),
            cancellationToken);
    }

    private async ValueTask ProcessPsyncAsync(
        RespValue message,
        CancellationToken cancellationToken) {
        PsyncCommand command =
            _parser.ParsePsync(
                message);

        ReplicationHandshakeResult result =
            _handshake.ProcessPsync(
                command);

        if (result.RequiresFullResynchronization) {
            await _fullResynchronizationCoordinator
                .ExecuteAsync(
                    cancellationToken);

            _handshake.Complete();

            return;
        }

        await _partialResynchronizationCoordinator
            .ExecuteAsync(
                result.ReplicationOffset,
                cancellationToken);

        _handshake.Complete();
    }
}