using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Server.Commands.Abstractions;
using System.Globalization;

namespace CacheVault.Server.Commands.Implementations;

public sealed class WaitCommand :
    IRedisCommand {
    private readonly IReplicationWaiter _replicationWaiter;

    public WaitCommand(
        IReplicationWaiter replicationWaiter) {
        ArgumentNullException.ThrowIfNull(
            replicationWaiter);

        _replicationWaiter =
            replicationWaiter;
    }

    public string Name =>
        "WAIT";

    public async ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        ArgumentNullException.ThrowIfNull(
            context);

        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 2) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'wait' command");
        }

        int replicaCount =
            ParseNonNegativeInt(
                arguments[0],
                "number of replicas");

        long timeoutMilliseconds =
            ParseNonNegativeLong(
                arguments[1],
                "timeout");

        long acknowledged =
            await _replicationWaiter.WaitAsync(
                replicaCount,
                TimeSpan.FromMilliseconds(
                    timeoutMilliseconds),
                context.CancellationToken);

        return new RespInteger(
            acknowledged);
    }

    private static int ParseNonNegativeInt(
        RespValue value,
        string parameterName) {
        string text =
            ExtractBulkString(
                value,
                parameterName);

        if (!int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result) ||
            result < 0) {
            throw new CommandArgumentException(
                $"ERR invalid {parameterName}");
        }

        return result;
    }

    private static long ParseNonNegativeLong(
        RespValue value,
        string parameterName) {
        string text =
            ExtractBulkString(
                value,
                parameterName);

        if (!long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long result) ||
            result < 0) {
            throw new CommandArgumentException(
                $"ERR invalid {parameterName}");
        }

        return result;
    }

    private static string ExtractBulkString(
        RespValue value,
        string parameterName) {
        if (value is not RespBulkString bulkString ||
            bulkString.Value is null) {
            throw new CommandArgumentException(
                $"ERR {parameterName} must be an integer");
        }

        return bulkString.Value;
    }
}