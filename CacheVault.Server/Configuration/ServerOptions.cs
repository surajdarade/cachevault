namespace CacheVault.Server.Configuration;

public sealed class ServerOptions {
    public const string SectionName = "Server";

    public string Host { get; set; } =
        "0.0.0.0";

    public int Port { get; set; } =
        6379;

    public bool IsReplica { get; set; }

    public string MasterHost { get; set; } =
        "127.0.0.1";

    public int MasterPort { get; set; } =
        6379;

    public bool EnableRdb { get; set; } =
        true;

    public string RdbFilePath { get; set; } =
        "cachevault.rdb";

    public bool EnableAof { get; set; } =
        true;

    public string AofFilePath { get; set; } =
        "cachevault.aof";
}