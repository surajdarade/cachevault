namespace CacheVault.Core.Models;

public sealed record PersistentList(
    string Key,
    IReadOnlyList<string> Values);
