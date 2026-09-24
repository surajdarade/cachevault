using System.Text;
using CacheVault.Protocol.Exceptions;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Replication.Protocol;

public sealed class ReplicationStreamReader {
    private const int InitialBufferSize = 8192;

    private readonly IRespParser _parser;

    private byte[] _buffer =
        new byte[InitialBufferSize];

    private int _start;

    private int _end;

    public ReplicationStreamReader(
        IRespParser parser) {
        ArgumentNullException.ThrowIfNull(
            parser);

        _parser =
            parser;
    }

    public async ValueTask<RespValue?> ReadRespAsync(
        Stream stream,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            stream);

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

    public async ValueTask<byte[]> ReadBulkPayloadAsync(
        Stream stream,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            stream);

        string header =
            await ReadLineAsync(
                stream,
                cancellationToken);

        if (header.Length < 2 ||
            header[0] != '$') {
            throw new FormatException(
                "Expected a RESP bulk payload length header.");
        }

        if (!long.TryParse(
                header.AsSpan(1),
                out long length)) {
            throw new FormatException(
                $"Invalid bulk payload length '{header[1..]}'.");
        }

        if (length < 0) {
            throw new FormatException(
                "Replication snapshot length cannot be negative.");
        }

        if (length > int.MaxValue) {
            throw new InvalidDataException(
                "Replication snapshot is too large to buffer in memory.");
        }

        byte[] payload =
            new byte[(int)length];

        await ReadExactlyAsync(
            stream,
            payload,
            cancellationToken);

        return payload;
    }

    public async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            stream);

        int destinationOffset = 0;

        while (destinationOffset < destination.Length) {
            int bufferedLength =
                _end - _start;

            if (bufferedLength > 0) {
                int bytesToCopy =
                    Math.Min(
                        bufferedLength,
                        destination.Length -
                        destinationOffset);

                _buffer.AsSpan(
                        _start,
                        bytesToCopy)
                    .CopyTo(
                        destination.Span[
                            destinationOffset..(
                                destinationOffset +
                                bytesToCopy)]);

                _start += bytesToCopy;

                destinationOffset += bytesToCopy;

                continue;
            }

            int bytesRead =
                await stream.ReadAsync(
                    destination[
                        destinationOffset..],
                    cancellationToken);

            if (bytesRead == 0) {
                throw new EndOfStreamException(
                    "Unexpected end of replication stream.");
            }

            destinationOffset += bytesRead;
        }
    }

    private async ValueTask<string> ReadLineAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        var bytes =
            new List<byte>();

        while (true) {
            byte value =
                await ReadByteAsync(
                    stream,
                    cancellationToken);

            if (value == '\n') {
                if (bytes.Count == 0 ||
                    bytes[^1] != '\r') {
                    throw new FormatException(
                        "Replication line must end with CRLF.");
                }

                bytes.RemoveAt(
                    bytes.Count - 1);

                return Encoding.ASCII.GetString(
                    bytes.ToArray());
            }

            bytes.Add(value);
        }
    }

    private async ValueTask<byte> ReadByteAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        if (_start < _end) {
            return _buffer[_start++];
        }

        int bytesRead =
            await ReadMoreAsync(
                stream,
                cancellationToken);

        if (bytesRead == 0) {
            throw new EndOfStreamException(
                "Unexpected end of replication stream.");
        }

        _end += bytesRead;

        return _buffer[_start++];
    }

    private async ValueTask<int> ReadMoreAsync(
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
            checked(
                _buffer.Length * 2);

        Array.Resize(
            ref _buffer,
            newSize);
    }
}