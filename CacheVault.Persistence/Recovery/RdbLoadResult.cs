namespace CacheVault.Persistence.Recovery;

public sealed record RdbLoadResult(
    int RecordsLoaded,
    long AofOffset);