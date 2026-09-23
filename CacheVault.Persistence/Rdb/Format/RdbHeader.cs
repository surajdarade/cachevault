namespace CacheVault.Persistence.Rdb.Format;

public sealed record RdbHeader(
    ushort Version) {
    public const string Magic = "CVDB";

    public const ushort CurrentVersion = 1;
}