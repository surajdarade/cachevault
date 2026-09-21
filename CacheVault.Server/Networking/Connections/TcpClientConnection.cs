using System.Net.Sockets;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class TcpClientConnection : IClientConnection {
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly IRespParser _parser;
    private readonly IRespSerializer _serializer;
    private readonly CommandDispatcher _dispatcher;

    public TcpClientConnection(
        TcpClient client,
        IRespParser parser,
        IRespSerializer serializer,
        CommandDispatcher dispatcher) {
        _client = client;
        _stream = client.GetStream();
        _parser = parser;
        _serializer = serializer;
        _dispatcher = dispatcher;
    }

    public ClientSession Session { get; } = new();

    public async Task RunAsync(
        CancellationToken cancellationToken) {
        byte[] buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested) {
            int bytesRead = await _stream.ReadAsync(
                buffer,
                cancellationToken);

            if (bytesRead == 0) {
                break;
            }

            ReadOnlySpan<byte> data =
                buffer.AsSpan(0, bytesRead);

            RespValue request;

            try {
                request = _parser.Parse(data);
            }
            catch (FormatException exception) {
                RespValue error =
                    new RespError(
                        $"ERR {exception.Message}");

                await WriteResponseAsync(
                    error,
                    cancellationToken);

                continue;
            }

            if (request is not RespArray command) {
                RespValue error =
                    new RespError(
                        "ERR command must be an array");

                await WriteResponseAsync(
                    error,
                    cancellationToken);

                continue;
            }

            RespValue response;

            try {
                CommandContext context =
                    new(
                        Session,
                        cancellationToken);

                response =
                    await _dispatcher.DispatchAsync(
                        context,
                        command);
            }
            catch (CommandArgumentException exception) {
                response =
                    new RespError(
                        exception.Message);
            }

            await WriteResponseAsync(
                response,
                cancellationToken);
        }
    }

    private async Task WriteResponseAsync(
        RespValue response,
        CancellationToken cancellationToken) {
        byte[] data =
            _serializer.Serialize(response);

        await _stream.WriteAsync(
            data,
            cancellationToken);
    }

    public void Dispose() {
        _stream.Dispose();
        _client.Dispose();
    }
}