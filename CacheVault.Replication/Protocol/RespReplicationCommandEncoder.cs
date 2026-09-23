using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Protocol;

public sealed class RespReplicationCommandEncoder :
    IReplicationCommandEncoder {
    private readonly IRespSerializer _serializer;

    public RespReplicationCommandEncoder(
        IRespSerializer serializer) {
        ArgumentNullException.ThrowIfNull(
            serializer);

        _serializer = serializer;
    }

    public ReplicationEntry Encode(
        long startOffset,
        string commandName,
        IReadOnlyList<string> arguments) {
        if (startOffset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(startOffset),
                "Replication start offset cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        var values =
            new List<RespValue>(
                arguments.Count + 1)
            {
                new RespBulkString(commandName)
            };

        foreach (string argument in arguments) {
            ArgumentNullException.ThrowIfNull(
                argument);

            values.Add(
                new RespBulkString(argument));
        }

        var command =
            new RespArray(values);

        byte[] data =
            _serializer.Serialize(
                command);

        long endOffset =
            checked(startOffset + data.Length);

        return new ReplicationEntry(
            startOffset,
            endOffset,
            data);
    }
}