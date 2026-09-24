using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class ClientConnectionHandler :
    IClientConnectionHandler {
    private readonly IRespSerializer _respSerializer;
    private readonly CommandDispatcher _commandDispatcher;

    public ClientConnectionHandler(
        IRespSerializer respSerializer,
        CommandDispatcher commandDispatcher) {
        ArgumentNullException.ThrowIfNull(
            respSerializer);

        ArgumentNullException.ThrowIfNull(
            commandDispatcher);

        _respSerializer =
            respSerializer;

        _commandDispatcher =
            commandDispatcher;
    }

    public async Task HandleAsync(
        TcpClient client,
        IRespStreamReader respStreamReader,
        RespValue? initialMessage,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(respStreamReader);

        using (client)
        using (NetworkStream stream = client.GetStream()) {
            var session =
                new ClientSession();

            if (initialMessage is not null) {
                await ProcessMessageAsync(
                    session,
                    initialMessage,
                    stream,
                    cancellationToken);
            }

            while (!cancellationToken.IsCancellationRequested) {
                RespValue? request =
                    await respStreamReader.ReadAsync(
                        stream,
                        cancellationToken);

                if (request is null) {
                    return;
                }

                await ProcessMessageAsync(
                    session,
                    request,
                    stream,
                    cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        ClientSession session,
        RespValue request,
        NetworkStream stream,
        CancellationToken cancellationToken) {
        if (request is not RespArray command) {
            RespValue response =
                new RespError(
                    "ERR expected command array");

            await WriteResponseAsync(
                stream,
                response,
                cancellationToken);

            return;
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

    private async Task WriteResponseAsync(
        NetworkStream stream,
        RespValue response,
        CancellationToken cancellationToken) {
        byte[] bytes =
            _respSerializer.Serialize(
                response);

        await stream.WriteAsync(
            bytes,
            cancellationToken);
    }
}