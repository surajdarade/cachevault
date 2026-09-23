using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.Replication.Master;

public sealed class ReplicationHandshake {
    private readonly ReplicaConnection _connection;
    private readonly IPsyncDecisionService _psyncDecisionService;

    private ReplicationHandshakeState _state =
        ReplicationHandshakeState.NotStarted;

    public ReplicationHandshake(
        ReplicaConnection connection,
        IPsyncDecisionService psyncDecisionService) {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            psyncDecisionService);

        _connection = connection;
        _psyncDecisionService =
            psyncDecisionService;
    }

    public ReplicationHandshakeState State =>
        _state;

    public string? ReplicaId =>
        _connection.Replica.ReplicaId;

    public void Start() {
        EnsureState(
            ReplicationHandshakeState.NotStarted);

        if (_connection.State !=
            ReplicaConnectionState.Connected) {
            throw new InvalidOperationException(
                "Replication handshake requires a connected replica.");
        }

        _connection.BeginHandshake();

        _state =
            ReplicationHandshakeState.WaitingForListeningPort;
    }

    public void ProcessReplConf(
        ReplConfCommand command) {
        ArgumentNullException.ThrowIfNull(
            command);

        switch (_state) {
            case ReplicationHandshakeState.WaitingForListeningPort:
                ProcessListeningPort(command);
                return;

            case ReplicationHandshakeState.WaitingForCapabilities:
                ProcessCapabilities(command);
                return;

            default:
                throw new InvalidOperationException(
                    $"REPLCONF cannot be processed while " +
                    $"handshake is in state '{_state}'.");
        }
    }

    public ReplicationHandshakeResult ProcessPsync(
        PsyncCommand command) {
        ArgumentNullException.ThrowIfNull(
            command);

        EnsureState(
            ReplicationHandshakeState.WaitingForPsync);

        PsyncDecision decision =
            _psyncDecisionService.Decide(
                command);

        if (decision.RequiresFullResynchronization) {
            _connection.BeginSynchronization();

            _state =
                ReplicationHandshakeState.FullResynchronization;

            return new ReplicationHandshakeResult(
                _state,
                _connection.Replica.ReplicaId,
                decision.RequestedOffset,
                true);
        }

        _connection.BeginSynchronization();

        _state =
            ReplicationHandshakeState.PartialResynchronization;

        return new ReplicationHandshakeResult(
            _state,
            _connection.Replica.ReplicaId,
            decision.RequestedOffset,
            false);
    }

    public void Complete() {
        if (_state !=
            ReplicationHandshakeState.FullResynchronization &&
            _state !=
            ReplicationHandshakeState.PartialResynchronization) {
            throw new InvalidOperationException(
                $"Handshake cannot be completed from state '{_state}'.");
        }

        if (_connection.State !=
            ReplicaConnectionState.Online) {
            _connection.MarkOnline();
        }

        _state =
            ReplicationHandshakeState.Completed;
    }

    public void Fail() {
        _state =
            ReplicationHandshakeState.Failed;

        _connection.MarkDisconnected();
    }

    private void ProcessListeningPort(
        ReplConfCommand command) {
        EnsureSubCommand(
            command,
            "listening-port");

        if (command.Arguments.Count != 1) {
            throw new FormatException(
                "REPLCONF listening-port requires exactly one argument.");
        }

        if (!int.TryParse(
                command.Arguments[0],
                out int port) ||
            port <= 0 ||
            port > 65535) {
            throw new FormatException(
                $"Invalid replica listening port '{command.Arguments[0]}'.");
        }

        _state =
            ReplicationHandshakeState.WaitingForCapabilities;
    }

    private void ProcessCapabilities(
        ReplConfCommand command) {
        EnsureSubCommand(
            command,
            "capa");

        if (command.Arguments.Count == 0) {
            throw new FormatException(
                "REPLCONF capa requires at least one capability.");
        }

        bool supportsPsync2 =
            command.Arguments.Any(
                argument =>
                    argument.Equals(
                        "psync2",
                        StringComparison.OrdinalIgnoreCase));

        if (!supportsPsync2) {
            throw new InvalidOperationException(
                "Replica does not advertise PSYNC2 capability.");
        }

        _state =
            ReplicationHandshakeState.WaitingForPsync;
    }

    private static void EnsureSubCommand(
        ReplConfCommand command,
        string expected) {
        if (!command.SubCommand.Equals(
                expected,
                StringComparison.OrdinalIgnoreCase)) {
            throw new FormatException(
                $"Expected REPLCONF {expected}.");
        }
    }

    private void EnsureState(
        ReplicationHandshakeState expected) {
        if (_state != expected) {
            throw new InvalidOperationException(
                $"Expected handshake state '{expected}', " +
                $"but current state is '{_state}'.");
        }
    }
}