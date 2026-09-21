using CacheVault.Protocol.Exceptions;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Networking.Abstractions;

namespace CacheVault.Server.Networking.Connections;

public sealed class RespStreamReader : IRespStreamReader {
    private const int InitialBufferSize = 4096;

    private readonly IRespParser _parser;

    private byte[] _buffer =
        new byte[InitialBufferSize];

    private int _start;
    private int _end;

    public RespStreamReader(
        IRespParser parser) {
        ArgumentNullException.ThrowIfNull(parser);

        _parser = parser;
    }

    public async ValueTask<RespValue?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(stream);

        while (true) {
            if (_parser.TryParse(
                    _buffer.AsSpan(
                        _start,
                        _end - _start),
                    out RespValue? value,
                    out int bytesConsumed)) {
                _start += bytesConsumed;

                return value;
            }

            int bytesRead =
                await ReadMoreAsync(
                    stream,
                    cancellationToken);

            if (bytesRead == 0) {
                if (_start == _end) {
                    return null;
                }

                throw new RespIncompleteException();
            }

            _end += bytesRead;
        }
    }

    private async Task<int> ReadMoreAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        EnsureWritableSpace();

        return await stream.ReadAsync(
            _buffer.AsMemory(_end),
            cancellationToken);
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
}