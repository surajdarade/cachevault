using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class ConnectionHandler :
    IConnectionHandler {
    private readonly IRespStreamReaderFactory _respStreamReaderFactory;
    private readonly IClientConnectionHandler _clientConnectionHandler;
    private readonly ReplicaConnectionHandler _replicaConnectionHandler;

    public ConnectionHandler(
        IRespStreamReaderFactory respStreamReaderFactory,
        IClientConnectionHandler clientConnectionHandler,
        ReplicaConnectionHandler replicaConnectionHandler) {
        ArgumentNullException.ThrowIfNull(
            respStreamReaderFactory);

        ArgumentNullException.ThrowIfNull(
            clientConnectionHandler);

        ArgumentNullException.ThrowIfNull(
            replicaConnectionHandler);

        _respStreamReaderFactory =
            respStreamReaderFactory;

        _clientConnectionHandler =
            clientConnectionHandler;

        _replicaConnectionHandler =
            replicaConnectionHandler;
    }

    public async Task HandleAsync(
        TcpClient client,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(client);

        IRespStreamReader reader =
            _respStreamReaderFactory.Create();

        NetworkStream stream =
            client.GetStream();

        RespValue? initialMessage =
            await reader.ReadAsync(
                stream,
                cancellationToken);

        if (initialMessage is null) {
            client.Dispose();
            return;
        }

        if (IsReplicationCommand(initialMessage)) {
            await _replicaConnectionHandler.HandleAsync(
                client,
                reader,
                initialMessage,
                cancellationToken);

            return;
        }

        await _clientConnectionHandler.HandleAsync(
            client,
            reader,
            initialMessage,
            cancellationToken);
    }

    private static bool IsReplicationCommand(
        RespValue message) {
        if (message is not RespArray array ||
            array.Values is null ||
            array.Values.Count == 0) {
            return false;
        }

        if (array.Values[0] is not RespBulkString command ||
            command.Value is null) {
            return false;
        }

        return command.Value.Equals(
                   "REPLCONF",
                   StringComparison.OrdinalIgnoreCase)
               ||
               command.Value.Equals(
                   "PSYNC",
                   StringComparison.OrdinalIgnoreCase);
    }
}