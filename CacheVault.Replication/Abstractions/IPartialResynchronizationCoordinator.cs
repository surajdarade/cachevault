namespace CacheVault.Replication.Abstractions;

public interface IPartialResynchronizationCoordinator {
    ValueTask ExecuteAsync(
        long requestedOffset,
        CancellationToken cancellationToken = default);
}