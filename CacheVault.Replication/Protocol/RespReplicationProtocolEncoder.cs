using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.Protocol;

public sealed class RespReplicationProtocolEncoder :
    IReplicationProtocolEncoder {
    private readonly IRespSerializer _serializer;

    public RespReplicationProtocolEncoder(
        IRespSerializer serializer) {
        ArgumentNullException.ThrowIfNull(serializer);

        _serializer = serializer;
    }

    public byte[] EncodeReplConf(
        ReplConfCommand command) {
        ArgumentNullException.ThrowIfNull(command);

        var values =
            new List<RespValue>(
                command.Arguments.Count + 2)
            {
                new RespBulkString("REPLCONF"),
                new RespBulkString(command.SubCommand)
            };

        foreach (string argument in command.Arguments) {
            ArgumentNullException.ThrowIfNull(argument);

            values.Add(
                new RespBulkString(argument));
        }

        return Serialize(
            new RespArray(values));
    }

    public byte[] EncodePsync(
        PsyncCommand command) {
        ArgumentNullException.ThrowIfNull(command);

        var values =
            new RespValue[]
            {
                new RespBulkString("PSYNC"),
                new RespBulkString(
                    command.ReplicationId),
                new RespBulkString(
                    command.Offset.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
            };

        return Serialize(
            new RespArray(values));
    }

    public byte[] EncodeFullResync(
        FullResyncResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        string value =
            $"FULLRESYNC " +
            $"{response.ReplicationId} " +
            $"{response.ReplicationOffset.ToString(
                System.Globalization.CultureInfo.InvariantCulture)}";

        return Serialize(
            new RespSimpleString(value));
    }

    public byte[] EncodeContinue() {
        return Serialize(
            new RespSimpleString("CONTINUE"));
    }

    public byte[] EncodeAck(
        ReplicationAck acknowledgement) {
        ArgumentNullException.ThrowIfNull(
            acknowledgement);

        var values =
            new RespValue[]
            {
                new RespBulkString("REPLCONF"),
                new RespBulkString("ACK"),
                new RespBulkString(
                    acknowledgement.Offset.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
            };

        return Serialize(
            new RespArray(values));
    }

    private byte[] Serialize(
        RespValue value) {
        return _serializer.Serialize(value);
    }
}