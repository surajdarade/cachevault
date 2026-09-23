using System.Runtime.InteropServices;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Persistence.Aof.Reading;

public sealed class AofCommandReader {
    private const int InitialBufferSize = 4096;
    private const int ReadBufferSize = 4096;

    private readonly IRespParser _parser;
    private readonly List<byte> _buffer;
    private readonly byte[] _readBuffer;

    public AofCommandReader(
        IRespParser parser) {
        ArgumentNullException.ThrowIfNull(
            parser);

        _parser = parser;
        _buffer = new List<byte>(
            InitialBufferSize);
        _readBuffer = new byte[
            ReadBufferSize];
    }

    /// <summary>
    /// Reads the next complete RESP command from the AOF.
    ///
    /// Returns null when the stream reaches a clean EOF
    /// without any remaining bytes.
    ///
    /// Throws EndOfStreamException when the stream ends
    /// while a command is only partially available.
    /// </summary>
    public RespArray? ReadCommand(
        Stream stream) {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (!stream.CanRead) {
            throw new ArgumentException(
                "The stream must be readable.",
                nameof(stream));
        }

        while (true) {
            if (TryParseCommand(
                    out RespArray? command,
                    out int bytesConsumed)) {
                RemoveConsumedBytes(
                    bytesConsumed);

                return command;
            }

            int bytesRead =
                stream.Read(
                    _readBuffer,
                    0,
                    _readBuffer.Length);

            if (bytesRead == 0) {
                if (_buffer.Count == 0) {
                    return null;
                }

                throw new InvalidDataException(
                    "AOF contains a truncated RESP command.");
            }

            for (int i = 0;
                 i < bytesRead;
                 i++) {
                _buffer.Add(
                    _readBuffer[i]);
            }
        }
    }

    private bool TryParseCommand(
        out RespArray? command,
        out int bytesConsumed) {
        command = null;
        bytesConsumed = 0;

        if (_buffer.Count == 0) {
            return false;
        }

        bool parsed =
            _parser.TryParse(
                CollectionsMarshal.AsSpan(
                    _buffer),
                out RespValue? value,
                out bytesConsumed);

        if (!parsed) {
            return false;
        }

        if (value is not RespArray array ||
            array.Values is null ||
            array.Values.Count == 0) {
            throw new FormatException(
                "AOF command must be a non-empty RESP array.");
        }

        command = array;

        return true;
    }

    private void RemoveConsumedBytes(
        int bytesConsumed) {
        if (bytesConsumed <= 0 ||
            bytesConsumed > _buffer.Count) {
            throw new InvalidOperationException(
                "RESP parser returned an invalid consumed byte count.");
        }

        _buffer.RemoveRange(
            0,
            bytesConsumed);
    }
}