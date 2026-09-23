namespace CacheVault.Persistence.Rdb.Format;

public sealed class RdbBinaryWriter {
    private readonly Stream _stream;

    public RdbBinaryWriter(
        Stream stream) {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanWrite) {
            throw new ArgumentException(
                "The stream must be writable.",
                nameof(stream));
        }

        _stream = stream;
    }

    public void WriteByte(
        byte value) {
        _stream.WriteByte(
            value);
    }

    public void WriteUInt16(
        ushort value) {
        Span<byte> buffer =
            stackalloc byte[sizeof(ushort)];

        buffer[0] =
            (byte)value;

        buffer[1] =
            (byte)(value >> 8);

        _stream.Write(
            buffer);
    }

    public void WriteUInt32(
        uint value) {
        Span<byte> buffer =
            stackalloc byte[sizeof(uint)];

        buffer[0] =
            (byte)value;

        buffer[1] =
            (byte)(value >> 8);

        buffer[2] =
            (byte)(value >> 16);

        buffer[3] =
            (byte)(value >> 24);

        _stream.Write(
            buffer);
    }

    public void WriteUInt64(
        ulong value) {
        Span<byte> buffer =
            stackalloc byte[sizeof(ulong)];

        buffer[0] =
            (byte)value;

        buffer[1] =
            (byte)(value >> 8);

        buffer[2] =
            (byte)(value >> 16);

        buffer[3] =
            (byte)(value >> 24);

        buffer[4] =
            (byte)(value >> 32);

        buffer[5] =
            (byte)(value >> 40);

        buffer[6] =
            (byte)(value >> 48);

        buffer[7] =
            (byte)(value >> 56);

        _stream.Write(
            buffer);
    }

    public void WriteInt64(
        long value) {
        WriteUInt64(
            unchecked(
                (ulong)value));
    }

    public void WriteBytes(
        ReadOnlySpan<byte> value) {
        _stream.Write(
            value);
    }
}