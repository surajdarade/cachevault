using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Protocol;

namespace CacheVault.Replication.Abstractions;

public interface IReplicationProtocolParser {
    ReplConfCommand ParseReplConf(
        RespValue value);

    PsyncCommand ParsePsync(
        RespValue value);

    FullResyncResponse ParseFullResync(
        RespValue value);

    bool IsContinue(
        RespValue value);

    ReplicationAck ParseAck(
        RespValue value);
}