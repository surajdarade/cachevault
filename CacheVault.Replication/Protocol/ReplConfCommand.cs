namespace CacheVault.Replication.Protocol;

public sealed record ReplConfCommand(
    string SubCommand,
    IReadOnlyList<string> Arguments);