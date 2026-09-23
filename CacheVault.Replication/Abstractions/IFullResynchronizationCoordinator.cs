namespace CacheVault.Replication.Abstractions;

public interface IFullResynchronizationCoordinator {
    ValueTask ExecuteAsync(
        CancellationToken cancellationToken = default);
}