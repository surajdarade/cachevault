using System.Net;
using System.Net.Sockets;
using System.Text;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.IntegrationTests.Infrastructure;

public sealed class RedisTestClient : IAsyncDisposable {
    private readonly TcpClient _client;

    private readonly NetworkStream _stream;

    private readonly IRespParser _parser;

    private byte[] _buffer =
        new byte[4096];

    private int _start;

    private int _end;

    private RedisTestClient(
        TcpClient client,
        IRespParser parser) {
        _client = client;

        _stream =
            client.GetStream();

        _parser = parser;
    }

    public static async Task<RedisTestClient> ConnectAsync(
        IPEndPoint endpoint) {
        ArgumentNullException.ThrowIfNull(endpoint);

        var client =
            new TcpClient();

        await client.ConnectAsync(
            endpoint.Address,
            endpoint.Port);

        return new RedisTestClient(
            client,
            new RespParser());
    }

    public async Task<RespValue> SendCommandAsync(
        params string[] arguments) {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Length == 0) {
            throw new ArgumentException(
                "At least one command argument is required.",
                nameof(arguments));
        }

        byte[] request =
            BuildCommand(arguments);

        await _stream.WriteAsync(
            request);

        RespValue? response =
            await ReadResponseAsync();

        return response;
    }

    public async Task SendRawCommandAsync(
    params string[] arguments) {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Length == 0) {
            throw new ArgumentException(
                "At least one command argument is required.",
                nameof(arguments));
        }

        byte[] request =
            BuildCommand(arguments);

        await _stream.WriteAsync(
            request);
    }

    private static byte[] BuildCommand(
        IReadOnlyList<string> arguments) {
        using var stream =
            new MemoryStream();

        WriteAscii(
            stream,
            $"*{arguments.Count}\r\n");

        foreach (string argument in arguments) {
            byte[] bytes =
                Encoding.UTF8.GetBytes(argument);

            WriteAscii(
                stream,
                $"${bytes.Length}\r\n");

            stream.Write(bytes);

            WriteAscii(
                stream,
                "\r\n");
        }

        return stream.ToArray();
    }

    public async Task<RespValue> ReadResponseAsync() {
        while (true) {
            if (_parser.TryParse(
                    _buffer.AsSpan(
                        _start,
                        _end - _start),
                    out RespValue? value,
                    out int bytesConsumed)) {
                _start += bytesConsumed;

                return value!;
            }

            EnsureWritableSpace();

            int bytesRead =
                await _stream.ReadAsync(
                    _buffer.AsMemory(_end));

            if (bytesRead == 0) {
                if (_start == _end) {
                    throw new InvalidOperationException(
                        "The server closed the connection before sending a response.");
                }

                throw new InvalidOperationException(
                    "The server closed the connection with an incomplete RESP response.");
            }

            _end += bytesRead;
        }
    }

    private void EnsureWritableSpace() {
        if (_end < _buffer.Length) {
            return;
        }

        if (_start > 0) {
            int bufferedLength =
                _end - _start;

            Buffer.BlockCopy(
                _buffer,
                _start,
                _buffer,
                0,
                bufferedLength);

            _start = 0;
            _end = bufferedLength;

            return;
        }

        int newSize =
            checked(_buffer.Length * 2);

        Array.Resize(
            ref _buffer,
            newSize);
    }

    private static void WriteAscii(
        Stream stream,
        string value) {
        byte[] bytes =
            Encoding.ASCII.GetBytes(value);

        stream.Write(bytes);
    }

    public ValueTask DisposeAsync() {
        _stream.Dispose();
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    public async Task SendRawAsync(
    byte[] data) {
        ArgumentNullException.ThrowIfNull(data);

        await _stream.WriteAsync(
            data);
    }
}