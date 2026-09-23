namespace CacheVault.Core.Abstractions;

public interface IClock {
    DateTimeOffset UtcNow { get; }
}