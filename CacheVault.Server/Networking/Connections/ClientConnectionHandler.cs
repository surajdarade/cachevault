using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Abstractions;
using CacheVault.Server.PubSub;

namespace CacheVault.Server.Networking.Connections;

public sealed class ClientConnectionHandler :
    IClientConnectionHandler {
    private readonly IRespSerializer _respSerializer;
    private readonly CommandDispatcher _commandDispatcher;
    private readonly PubSubManager _pubSubManager;

    public ClientConnectionHandler(
        IRespSerializer respSerializer,
        CommandDispatcher commandDispatcher,
        PubSubManager pubSubManager) {
        ArgumentNullException.ThrowIfNull(respSerializer);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(pubSubManager);

        _respSerializer = respSerializer;
        _commandDispatcher = commandDispatcher;
        _pubSubManager = pubSubManager;
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
            var session = new ClientSession();

            Task writerTask =
                WriteOutboundMessagesAsync(
                    session,
                    stream,
                    cancellationToken);

            try {
                if (initialMessage is not null) {
                    await ProcessMessageAsync(
                        session,
                        initialMessage,
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
                        cancellationToken);
                }
            }
            finally {
                _pubSubManager.RemoveSession(session);
                session.CompleteOutboundMessages();

                try {
                    await writerTask;
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested) {
                }
            }
        }
    }

    private async Task ProcessMessageAsync(
        ClientSession session,
        RespValue request,
        CancellationToken cancellationToken) {
        if (request is not RespArray command) {
            session.TryEnqueue(
                new RespError(
                    "ERR expected command array"));

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

            session.TryEnqueue(response);
        }
        catch (CommandArgumentException exception) {
            session.TryEnqueue(
                new RespError(
                    exception.Message));
        }
    }

    private async Task WriteOutboundMessagesAsync(
        ClientSession session,
        NetworkStream stream,
        CancellationToken cancellationToken) {
        await foreach (RespValue response in
            session.OutboundMessages.ReadAllAsync(
                cancellationToken)) {
            byte[] bytes =
                _respSerializer.Serialize(response);

            await stream.WriteAsync(
                bytes,
                cancellationToken);
        }
    }
}
