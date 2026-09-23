using CacheVault.Replication.Protocol;

namespace CacheVault.Replication.Abstractions;

public interface IReplicationProtocolEncoder {
    byte[] EncodeReplConf(
        ReplConfCommand command);

    byte[] EncodePsync(
        PsyncCommand command);

    byte[] EncodeFullResync(
        FullResyncResponse response);

    byte[] EncodeContinue();

    byte[] EncodeAck(
        ReplicationAck acknowledgement);
}