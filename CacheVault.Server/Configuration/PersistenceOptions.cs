namespace CacheVault.Server.Configuration;

public sealed class PersistenceOptions {
    public string RdbFilePath { get; set; } =
        "cachevault.rdb";

    public string AofFilePath { get; set; } =
        "cachevault.aof";

    public bool EnableRdb { get; set; } = true;

    public bool EnableAof { get; set; } = true;
}