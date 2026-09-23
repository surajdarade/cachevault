namespace CacheVault.Replication.Abstractions;

public interface IReplicationTransport :
    IAsyncDisposable {
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);
}