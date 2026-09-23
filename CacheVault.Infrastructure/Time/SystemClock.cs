using CacheVault.Core.Abstractions;

namespace CacheVault.Infrastructure.Time;

public sealed class SystemClock : IClock {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}