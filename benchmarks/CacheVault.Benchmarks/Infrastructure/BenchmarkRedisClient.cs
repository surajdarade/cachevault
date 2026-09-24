using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Benchmarks.Infrastructure;

public class BenchmarkRedisClient : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly RespParser _parser = new();

    private byte[] _readBuffer = new byte[64 * 1024];
    private int _readStart;
    private int _readEnd;

    private BenchmarkRedisClient(
        TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    public static async Task<BenchmarkRedisClient> ConnectAsync(
        IPEndPoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var client = new TcpClient
        {
            NoDelay = true
        };

        await client.ConnectAsync(
            endpoint.Address,
            endpoint.Port,
            cancellationToken).ConfigureAwait(false);

        return new BenchmarkRedisClient(client);
    }

    public async Task<RespValue> SendAsync(
        byte[] request,
        CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(
            request,
            cancellationToken).ConfigureAwait(false);

        return await ReadResponseAsync(
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SendPipelineAsync(
        byte[] request,
        int responseCount,
        CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(
            request,
            cancellationToken).ConfigureAwait(false);

        for (int index = 0; index < responseCount; index++)
        {
            await ReadResponseAsync(
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<RespValue> ReadResponseAsync(
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_parser.TryParse(
                    _readBuffer.AsSpan(
                        _readStart,
                        _readEnd - _readStart),
                    out RespValue? value,
                    out int bytesConsumed))
            {
                _readStart += bytesConsumed;

                if (_readStart == _readEnd)
                {
                    _readStart = 0;
                    _readEnd = 0;
                }

                return value!;
            }

            EnsureWritableSpace();

            int bytesRead =
                await _stream.ReadAsync(
                    _readBuffer.AsMemory(_readEnd),
                    cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                throw new IOException(
                    "CacheVault closed the benchmark connection.");
            }

            _readEnd += bytesRead;
        }
    }

    private void EnsureWritableSpace()
    {
        if (_readEnd < _readBuffer.Length)
        {
            return;
        }

        if (_readStart > 0)
        {
            int buffered =
                _readEnd - _readStart;

            Buffer.BlockCopy(
                _readBuffer,
                _readStart,
                _readBuffer,
                0,
                buffered);

            _readStart = 0;
            _readEnd = buffered;

            return;
        }

        int newSize =
            checked(_readBuffer.Length * 2);

        Array.Resize(
            ref _readBuffer,
            newSize);
    }

    public static byte[] Command(
        params string[] arguments)
    {
        using var stream =
            new MemoryStream();

        WriteAscii(
            stream,
            $"*{arguments.Length}\r\n");

        foreach (string argument in arguments)
        {
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

    public static byte[] Pipeline(
        IReadOnlyList<byte[]> commands)
    {
        int totalLength = 0;

        foreach (byte[] command in commands)
        {
            totalLength =
                checked(totalLength + command.Length);
        }

        byte[] pipeline =
            GC.AllocateUninitializedArray<byte>(
                totalLength);

        int offset = 0;

        foreach (byte[] command in commands)
        {
            command.AsSpan().CopyTo(
                pipeline.AsSpan(offset));

            offset += command.Length;
        }

        return pipeline;
    }

    private static void WriteAscii(
        Stream stream,
        string value)
    {
        byte[] bytes =
            Encoding.ASCII.GetBytes(value);

        stream.Write(bytes);
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        _client.Dispose();

        return ValueTask.CompletedTask;
    }
}
