using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class PsyncDecisionService :
    IPsyncDecisionService {
    private readonly IReplicationState _state;
    private readonly IReplicationBacklog _backlog;

    public PsyncDecisionService(
        IReplicationState state,
        IReplicationBacklog backlog) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(backlog);

        _state = state;
        _backlog = backlog;
    }

    public PsyncDecision Decide(
        PsyncCommand command) {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ReplicationId == "?" ||
            !string.Equals(
                command.ReplicationId,
                _state.ReplicationId,
                StringComparison.Ordinal)) {
            return FullResynchronization(
                command.Offset);
        }

        if (command.Offset < 0) {
            return FullResynchronization(
                command.Offset);
        }

        if (command.Offset > _state.ReplicationOffset) {
            return FullResynchronization(
                command.Offset);
        }

        if (!_backlog.TryReadFrom(
                command.Offset,
                out byte[] data)) {
            return FullResynchronization(
                command.Offset);
        }

        return new PsyncDecision(
            false,
            command.Offset,
            data);
    }

    private static PsyncDecision FullResynchronization(
        long requestedOffset) {
        return new PsyncDecision(
            true,
            requestedOffset,
            []);
    }
}