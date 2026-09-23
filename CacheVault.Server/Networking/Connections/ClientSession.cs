namespace CacheVault.Server.Networking.Connections;

public sealed class ClientSession {
    private readonly List<QueuedCommand> _queuedCommands =
        [];

    public Guid ConnectionId { get; } =
        Guid.NewGuid();

    public bool IsInTransaction { get; private set; }

    public IReadOnlyList<QueuedCommand> QueuedCommands =>
        _queuedCommands;

    public void BeginTransaction() {
        if (IsInTransaction) {
            throw new InvalidOperationException(
                "Transaction is already active.");
        }

        _queuedCommands.Clear();
        IsInTransaction = true;
    }

    public void QueueCommand(
        QueuedCommand command) {
        ArgumentNullException.ThrowIfNull(
            command);

        if (!IsInTransaction) {
            throw new InvalidOperationException(
                "No transaction is active.");
        }

        _queuedCommands.Add(
            command);
    }

    public IReadOnlyList<QueuedCommand> DrainTransaction() {
        if (!IsInTransaction) {
            throw new InvalidOperationException(
                "No transaction is active.");
        }

        QueuedCommand[] commands =
            _queuedCommands.ToArray();

        _queuedCommands.Clear();
        IsInTransaction = false;

        return commands;
    }

    public void DiscardTransaction() {
        if (!IsInTransaction) {
            throw new InvalidOperationException(
                "No transaction is active.");
        }

        _queuedCommands.Clear();
        IsInTransaction = false;
    }
}