using System.Globalization;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Server.Commands.Abstractions;

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

    public string Name => "WAIT";

    public async ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments) {
        if (arguments.Count != 2) {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'wait' command");
        }

        int replicaCount =
            ParseNonNegativeInteger(
                arguments[0],
                "replica count");

        long timeoutMilliseconds =
            ParseNonNegativeInteger(
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

    private static int ParseNonNegativeInteger(
        RespValue value,
        string argumentName) {
        if (value is not RespBulkString bulkString ||
            bulkString.Value is null) {
            throw new CommandArgumentException(
                $"ERR invalid {argumentName}");
        }

        if (!int.TryParse(
                bulkString.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result) ||
            result < 0) {
            throw new CommandArgumentException(
                $"ERR invalid {argumentName}");
        }

        return result;
    }
}