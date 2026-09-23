using System.Globalization;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;

namespace CacheVault.Replication.Protocol;

public sealed class RespReplicationProtocolParser :
    IReplicationProtocolParser {
    public ReplConfCommand ParseReplConf(
        RespValue value) {
        IReadOnlyList<string> values =
            GetBulkStringArray(
                value,
                "REPLCONF");

        if (values.Count < 2) {
            throw new FormatException(
                "REPLCONF requires a sub-command.");
        }

        return new ReplConfCommand(
            values[1],
            values.Skip(2).ToArray());
    }

    public PsyncCommand ParsePsync(
        RespValue value) {
        IReadOnlyList<string> values =
            GetBulkStringArray(
                value,
                "PSYNC");

        if (values.Count != 3) {
            throw new FormatException(
                "PSYNC requires replication ID and offset.");
        }

        if (!long.TryParse(
                values[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long offset)) {
            throw new FormatException(
                $"Invalid PSYNC offset '{values[2]}'.");
        }

        return new PsyncCommand(
            values[1],
            offset);
    }

    public FullResyncResponse ParseFullResync(
        RespValue value) {
        if (value is not RespSimpleString simpleString) {
            throw new FormatException(
                "FULLRESYNC response must be a simple string.");
        }

        string[] parts =
            simpleString.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3 ||
            !parts[0].Equals(
                "FULLRESYNC",
                StringComparison.OrdinalIgnoreCase)) {
            throw new FormatException(
                "Invalid FULLRESYNC response.");
        }

        if (!long.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long offset)) {
            throw new FormatException(
                $"Invalid FULLRESYNC offset '{parts[2]}'.");
        }

        return new FullResyncResponse(
            parts[1],
            offset);
    }

    public bool IsContinue(
        RespValue value) {
        if (value is not RespSimpleString simpleString) {
            return false;
        }

        return simpleString.Value.Equals(
            "CONTINUE",
            StringComparison.OrdinalIgnoreCase);
    }

    public ReplicationAck ParseAck(
        RespValue value) {
        IReadOnlyList<string> values =
            GetBulkStringArray(
                value,
                "REPLCONF");

        if (values.Count != 3 ||
            !values[1].Equals(
                "ACK",
                StringComparison.OrdinalIgnoreCase)) {
            throw new FormatException(
                "Invalid REPLCONF ACK message.");
        }

        if (!long.TryParse(
                values[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long offset)) {
            throw new FormatException(
                $"Invalid ACK offset '{values[2]}'.");
        }

        return new ReplicationAck(offset);
    }

    private static IReadOnlyList<string> GetBulkStringArray(
        RespValue value,
        string expectedCommand) {
        if (value is not RespArray array ||
            array.Values is null) {
            throw new FormatException(
                $"{expectedCommand} must be a RESP array.");
        }

        var values =
            new List<string>(
                array.Values.Count);

        foreach (RespValue item in array.Values) {
            if (item is not RespBulkString bulkString ||
                bulkString.Value is null) {
                throw new FormatException(
                    $"{expectedCommand} arguments must be non-null bulk strings.");
            }

            values.Add(
                bulkString.Value);
        }

        if (values.Count == 0 ||
            !values[0].Equals(
                expectedCommand,
                StringComparison.OrdinalIgnoreCase)) {
            throw new FormatException(
                $"Expected {expectedCommand} command.");
        }

        return values;
    }
}