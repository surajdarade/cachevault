namespace CacheVault.Server.Recovery;

public sealed record RecoveryResult(
    int RdbRecordsLoaded,
    int AofCommandsReplayed);