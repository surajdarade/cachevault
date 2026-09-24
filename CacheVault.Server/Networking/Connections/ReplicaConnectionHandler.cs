using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class ReplicaConnectionHandler {
    private readonly IReplicationState _replicationState;
    private readonly IReplicationBacklog _replicationBacklog;
    private readonly IReplicationSnapshotProvider _snapshotProvider;
    private readonly IReplicationProtocolParser _protocolParser;
    private readonly IReplicaConnectionRegistry _connectionRegistry;

    public ReplicaConnectionHandler(
        IReplicationState replicationState,
        IReplicationBacklog replicationBacklog,
        IReplicationSnapshotProvider snapshotProvider,
        IReplicationProtocolParser protocolParser,
        IReplicaConnectionRegistry connectionRegistry) {
        ArgumentNullException.ThrowIfNull(
            replicationState);

        ArgumentNullException.ThrowIfNull(
            replicationBacklog);

        ArgumentNullException.ThrowIfNull(
            snapshotProvider);

        ArgumentNullException.ThrowIfNull(
            protocolParser);

        ArgumentNullException.ThrowIfNull(
            connectionRegistry);

        _replicationState =
            replicationState;

        _replicationBacklog =
            replicationBacklog;

        _snapshotProvider =
            snapshotProvider;

        _protocolParser =
            protocolParser;

        _connectionRegistry =
            connectionRegistry;
    }

    public async Task HandleAsync(
        TcpClient client,
        IRespStreamReader reader,
        RespValue initialMessage,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(initialMessage);

        cancellationToken.ThrowIfCancellationRequested();

        await using TcpClientConnectionLifetime lifetime =
            new(client);

        NetworkStream stream =
            lifetime.Stream;

        var transport =
            new TcpReplicationTransport(stream);

        var replica =
            new ReplicaInfo(
                Guid.NewGuid().ToString("N"));

        var connection =
            new ReplicaConnection(
                replica,
                transport);

        connection.MarkConnected();

        var psyncDecisionService =
            new PsyncDecisionService(
                _replicationState,
                _replicationBacklog);

        var handshake =
            new ReplicationHandshake(
                connection,
                psyncDecisionService);

        var fullResynchronizationCoordinator =
            new FullResynchronizationCoordinator(
                connection,
                _snapshotProvider,
                _replicationState,
                _replicationBacklog);

        var partialResynchronizationCoordinator =
            new PartialResynchronizationCoordinator(
                connection,
                _replicationBacklog);

        var session =
            new ReplicaReplicationSession(
                connection,
                handshake,
                fullResynchronizationCoordinator,
                partialResynchronizationCoordinator,
                _protocolParser);

        try {
            session.Start();

            await ProcessMessageAsync(
                session,
                initialMessage,
                cancellationToken);

            while (!cancellationToken.IsCancellationRequested) {
                RespValue? message =
                    await reader.ReadAsync(
                        stream,
                        cancellationToken);

                if (message is null) {
                    return;
                }

                await ProcessMessageAsync(
                    session,
                    message,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
        }
        finally {
            connection.MarkDisconnected();

            _connectionRegistry.Remove(
                replica.ReplicaId);

            await connection.DisposeAsync();
        }
    }

    private static async ValueTask ProcessMessageAsync(
        ReplicaReplicationSession session,
        RespValue message,
        CancellationToken cancellationToken) {
        await session.ProcessAsync(
            message,
            cancellationToken);

        if (session.State ==
            ReplicationHandshakeState.Completed) {
            return;
        }
    }

    private sealed class TcpClientConnectionLifetime :
        IAsyncDisposable {
        private readonly TcpClient _client;

        public TcpClientConnectionLifetime(
            TcpClient client) {
            ArgumentNullException.ThrowIfNull(
                client);

            _client = client;

            Stream =
                client.GetStream();
        }

        public NetworkStream Stream { get; }

        public ValueTask DisposeAsync() {
            Stream.Dispose();
            _client.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}