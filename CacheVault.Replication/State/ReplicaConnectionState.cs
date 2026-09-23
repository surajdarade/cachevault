namespace CacheVault.Replication.State;

public enum ReplicaConnectionState {
    Disconnected = 0,
    Connected = 1,
    Handshaking = 2,
    Synchronizing = 3,
    Online = 4
}