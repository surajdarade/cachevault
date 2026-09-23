using System.Net.Sockets;
using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.Protocol;

public sealed class TcpReplicationTransport :
    IReplicationTransport {
    private readonly NetworkStream _stream;

    public TcpReplicationTransport(
        NetworkStream stream) {
        ArgumentNullException.ThrowIfNull(
            stream);

        _stream = stream;
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        return _stream.WriteAsync(
            data,
            cancellationToken);
    }

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        return _stream.ReadAsync(
            buffer,
            cancellationToken);
    }

    public ValueTask DisposeAsync() {
        return _stream.DisposeAsync();
    }
}