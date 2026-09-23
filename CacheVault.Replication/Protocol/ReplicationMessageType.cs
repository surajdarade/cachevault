namespace CacheVault.Replication.Protocol;

public enum ReplicationMessageType {
    ReplConf,
    Psync,
    FullResync,
    Continue,
    Ack
}