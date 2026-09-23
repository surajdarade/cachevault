using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.State;

public sealed class ReplicationBacklog :
    IReplicationBacklog {
    private readonly byte[] _buffer;
    private readonly object _sync = new();

    private long _firstOffset;
    private long _endOffset;
    private int _length;
    private bool _initialized;

    public ReplicationBacklog(
        long capacity) {
        if (capacity <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Replication backlog capacity must be greater than zero.");
        }

        if (capacity > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Replication backlog capacity cannot exceed Int32.MaxValue.");
        }

        Capacity =
            capacity;

        _buffer =
            new byte[(int)capacity];
    }

    public long FirstOffset {
        get {
            lock (_sync) {
                return _firstOffset;
            }
        }
    }

    public long EndOffset {
        get {
            lock (_sync) {
                return _endOffset;
            }
        }
    }

    public long Length {
        get {
            lock (_sync) {
                return _length;
            }
        }
    }

    public long Capacity { get; }

    public void Append(
        ReplicationEntry entry) {
        ArgumentNullException.ThrowIfNull(
            entry);

        lock (_sync) {
            if (!_initialized) {
                _firstOffset =
                    entry.StartOffset;

                _endOffset =
                    entry.StartOffset;

                _initialized = true;
            }

            if (entry.StartOffset != _endOffset) {
                throw new InvalidOperationException(
                    "Replication entries must be appended contiguously.");
            }

            if (entry.Length == 0) {
                return;
            }

            if (entry.Length >= Capacity) {
                long newFirstOffset =
                    entry.EndOffset -
                    Capacity;

                int sourceOffset =
                    entry.Data.Length -
                    (int)Capacity;

                WriteBytes(
                    newFirstOffset,
                    entry.Data,
                    sourceOffset,
                    (int)Capacity);

                _length =
                    (int)Capacity;

                _firstOffset =
                    newFirstOffset;

                _endOffset =
                    entry.EndOffset;

                return;
            }

            WriteBytes(
                entry.StartOffset,
                entry.Data,
                0,
                entry.Data.Length);

            _length +=
                entry.Data.Length;

            _endOffset =
                entry.EndOffset;

            if (_length > Capacity) {
                int overflow =
                    _length -
                    (int)Capacity;

                _length =
                    (int)Capacity;

                _firstOffset +=
                    overflow;
            }
        }
    }

    public bool TryReadFrom(
        long offset,
        out byte[] data) {
        lock (_sync) {
            if (!_initialized ||
                offset < _firstOffset ||
                offset > _endOffset) {
                data = [];

                return false;
            }

            if (offset == _endOffset) {
                data = [];

                return true;
            }

            long requestedLength =
                _endOffset -
                offset;

            if (requestedLength > int.MaxValue) {
                throw new InvalidOperationException(
                    "Requested replication data exceeds the maximum supported buffer size.");
            }

            int length =
                (int)requestedLength;

            data =
                new byte[length];

            int readPosition =
                GetBufferIndex(
                    offset);

            int firstChunk =
                Math.Min(
                    length,
                    _buffer.Length -
                    readPosition);

            Buffer.BlockCopy(
                _buffer,
                readPosition,
                data,
                0,
                firstChunk);

            int remaining =
                length -
                firstChunk;

            if (remaining > 0) {
                Buffer.BlockCopy(
                    _buffer,
                    0,
                    data,
                    firstChunk,
                    remaining);
            }

            return true;
        }
    }

    private void WriteBytes(
        long offset,
        byte[] source,
        int sourceOffset,
        int count) {
        int writePosition =
            GetBufferIndex(
                offset);

        int firstChunk =
            Math.Min(
                count,
                _buffer.Length -
                writePosition);

        Buffer.BlockCopy(
            source,
            sourceOffset,
            _buffer,
            writePosition,
            firstChunk);

        int remaining =
            count -
            firstChunk;

        if (remaining > 0) {
            Buffer.BlockCopy(
                source,
                sourceOffset + firstChunk,
                _buffer,
                0,
                remaining);
        }
    }

    private int GetBufferIndex(
        long offset) {
        return (int)(
            offset %
            _buffer.Length);
    }
}