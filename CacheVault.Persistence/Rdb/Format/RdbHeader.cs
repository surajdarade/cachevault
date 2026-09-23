namespace CacheVault.Persistence.Rdb.Format;

public sealed record RdbHeader(
    ushort Version,
    long AofOffset = 0) {
    public const string Magic = "CVDB";

    public const ushort CurrentVersion = 3;
}