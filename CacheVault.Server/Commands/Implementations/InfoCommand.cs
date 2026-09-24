using System.Text;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Configuration;
using CacheVault.Server.Replication;

namespace CacheVault.Server.Commands.Implementations;

public sealed class InfoCommand : IRedisCommand
{
    private readonly IReplicationState _replicationState;
    private readonly IReplicaRegistry _replicaRegistry;
    private readonly ServerOptions _options;
    private readonly ReplicaSynchronizationStatus _status;

    public InfoCommand(
        IReplicationState replicationState,
        IReplicaRegistry replicaRegistry,
        ServerOptions options,
        ReplicaSynchronizationStatus status)
    {
        ArgumentNullException.ThrowIfNull(replicationState);
        ArgumentNullException.ThrowIfNull(replicaRegistry);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(status);

        _replicationState = replicationState;
        _replicaRegistry = replicaRegistry;
        _options = options;
        _status = status;
    }

    public string Name => "INFO";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments)
    {
        if (arguments.Count > 1)
        {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'info' command");
        }

        RespBulkString? section =
            arguments.Count == 1
                ? arguments[0] as RespBulkString
                : null;

        if (arguments.Count == 1 &&
            section is null)
        {
            throw new CommandArgumentException(
                "ERR syntax error");
        }

        if (section?.Value is not null &&
            !section.Value.Equals(
                "replication",
                StringComparison.OrdinalIgnoreCase) &&
            !section.Value.Equals(
                "all",
                StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<RespValue>(
                new RespBulkString(string.Empty));
        }

        return ValueTask.FromResult<RespValue>(
            new RespBulkString(
                BuildReplicationInfo()));
    }

    private string BuildReplicationInfo()
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Replication");

        if (_replicationState.IsMaster)
        {
            builder.AppendLine("role:master");
            builder.Append("connected_slaves:");
            builder.AppendLine(
                _replicaRegistry.Count.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            builder.AppendLine("role:slave");
            builder.Append("master_host:");
            builder.AppendLine(_options.MasterHost);
            builder.Append("master_port:");
            builder.AppendLine(
                _options.MasterPort.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("master_link_status:");
            builder.AppendLine(
                _status.Connected
                    ? "up"
                    : "down");
            builder.Append("master_replid:");
            builder.AppendLine(_status.ReplicationId);
            builder.Append("master_repl_offset:");
            builder.AppendLine(
                _status.ReplicationOffset.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.Append("master_replid:");
        builder.AppendLine(_replicationState.ReplicationId);

        builder.Append("master_repl_offset:");
        builder.AppendLine(
            _replicationState.ReplicationOffset.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

        return builder.ToString();
    }
}
