namespace CacheVault.Replication.State;

public enum ReplicationHandshakeState {
    NotStarted = 0,
    WaitingForListeningPort = 1,
    WaitingForCapabilities = 2,
    WaitingForPsync = 3,
    FullResynchronization = 4,
    PartialResynchronization = 5,
    Completed = 6,
    Failed = 7
}