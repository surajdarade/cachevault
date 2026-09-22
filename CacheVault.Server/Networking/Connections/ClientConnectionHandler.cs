using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class ClientConnectionHandler : IClientConnectionHandler {
    private readonly IRespStreamReaderFactory _respStreamReaderFactory;

    private readonly IRespSerializer _respSerializer;

    private readonly CommandDispatcher _commandDispatcher;

    public ClientConnectionHandler(
        IRespStreamReaderFactory respStreamReaderFactory,
        IRespSerializer respSerializer,
        CommandDispatcher commandDispatcher) {
        ArgumentNullException.ThrowIfNull(
            respStreamReaderFactory);

        ArgumentNullException.ThrowIfNull(
            respSerializer);

        ArgumentNullException.ThrowIfNull(
            commandDispatcher);

        _respStreamReaderFactory =
            respStreamReaderFactory;

        _respSerializer =
            respSerializer;

        _commandDispatcher =
            commandDispatcher;
    }

    public async Task HandleAsync(
        TcpClient client,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(client);

        IRespStreamReader respStreamReader =
            _respStreamReaderFactory.Create();

        using (client)
        using (NetworkStream stream =
            client.GetStream()) {
            var session =
                new ClientSession();

            while (!cancellationToken.IsCancellationRequested) {
                RespValue? request =
                    await respStreamReader.ReadAsync(
                        stream,
                        cancellationToken);

                if (request is null) {
                    return;
                }

                if (request is not RespArray command) {
                    RespValue response =
                        new RespError(
                            "ERR expected command array");

                    await WriteResponseAsync(
                        stream,
                        response,
                        cancellationToken);

                    continue;
                }

                try {
                    var context =
                        new CommandContext(
                            session,
                            cancellationToken);

                    RespValue response =
                        await _commandDispatcher.DispatchAsync(
                            context,
                            command);

                    await WriteResponseAsync(
                        stream,
                        response,
                        cancellationToken);
                }
                catch (CommandArgumentException exception) {
                    RespValue response =
                        new RespError(
                            exception.Message);

                    await WriteResponseAsync(
                        stream,
                        response,
                        cancellationToken);
                }
            }
        }
    }

    private async Task WriteResponseAsync(
        NetworkStream stream,
        RespValue response,
        CancellationToken cancellationToken) {
        byte[] bytes =
            _respSerializer.Serialize(response);

        await stream.WriteAsync(
            bytes,
            cancellationToken);
    }
}