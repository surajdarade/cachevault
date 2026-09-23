using CacheVault.Core.Abstractions;

namespace CacheVault.UnitTests.CacheVault.Infrastructure;

public sealed class TestClock : IClock {
    public TestClock(
        DateTimeOffset initialTime) {
        UtcNow =
            initialTime;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(
        TimeSpan amount) {
        if (amount < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Clock cannot move backwards.");
        }

        UtcNow =
            UtcNow.Add(amount);
    }

    public void Set(
        DateTimeOffset value) {
        UtcNow =
            value;
    }
}