using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Configuration;
using CacheVault.Server.Replication;

namespace CacheVault.Server.Commands.Implementations;

public sealed class RoleCommand : IRedisCommand
{
    private readonly IReplicationState _replicationState;
    private readonly IReplicaRegistry _replicaRegistry;
    private readonly ServerOptions _options;
    private readonly ReplicaSynchronizationStatus _status;

    public RoleCommand(
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

    public string Name => "ROLE";

    public ValueTask<RespValue> ExecuteAsync(
        CommandContext context,
        IReadOnlyList<RespValue> arguments)
    {
        if (arguments.Count != 0)
        {
            throw new CommandArgumentException(
                "ERR wrong number of arguments for 'role' command");
        }

        if (_replicationState.IsMaster)
        {
            var replicas =
                new List<RespValue>
                {
                    new RespBulkString("master"),
                    new RespInteger(
                        _replicationState.ReplicationOffset)
                };

            foreach (var replica in _replicaRegistry.GetAll())
            {
                replicas.Add(
                    new RespArray(
                        [
                            new RespBulkString(replica.ReplicaId),
                            new RespBulkString(_options.Host),
                            new RespInteger(_options.Port),
                            new RespInteger(replica.AcknowledgedOffset)
                        ]));
            }

            return ValueTask.FromResult<RespValue>(
                new RespArray(replicas));
        }

        return ValueTask.FromResult<RespValue>(
            new RespArray(
                [
                    new RespBulkString("slave"),
                    new RespBulkString(_options.MasterHost),
                    new RespInteger(_options.MasterPort),
                    new RespBulkString(
                        _status.Connected
                            ? "connected"
                            : "disconnected"),
                    new RespInteger(
                        _status.ReplicationOffset)
                ]));
    }
}
