using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Persistence.Aof.Writing;

public sealed class AofCommandWriter {
    private readonly IRespSerializer _serializer;

    public AofCommandWriter(
        IRespSerializer serializer) {
        ArgumentNullException.ThrowIfNull(serializer);

        _serializer = serializer;
    }

    public void WriteCommand(
        Stream stream,
        string commandName,
        IReadOnlyList<string> arguments) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (!stream.CanWrite) {
            throw new ArgumentException(
                "The stream must be writable.",
                nameof(stream));
        }

        var values =
            new List<RespValue>(
                arguments.Count + 1)
            {
                new RespBulkString(
                    commandName)
            };

        foreach (string argument in arguments) {
            ArgumentNullException.ThrowIfNull(
                argument);

            values.Add(
                new RespBulkString(
                    argument));
        }

        var command =
            new RespArray(
                values);

        byte[] serialized =
            _serializer.Serialize(
                command);

        stream.Write(
            serialized);
    }
}