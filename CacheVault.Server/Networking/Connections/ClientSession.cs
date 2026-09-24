using System.Threading.Channels;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Server.Networking.Connections;

public sealed class ClientSession {
    private readonly List<QueuedCommand> _queuedCommands =
        [];

    private readonly WatchState _watchState =
        new();

    private readonly HashSet<string> _subscriptions =
        new(StringComparer.Ordinal);

    private readonly object _subscriptionLock =
        new();

    private readonly Channel<RespValue> _outboundMessages =
        Channel.CreateUnbounded<RespValue>(
            new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

    public Guid ConnectionId { get; } =
        Guid.NewGuid();

    public bool IsInTransaction { get; private set; }

    public IReadOnlyList<QueuedCommand> QueuedCommands =>
        _queuedCommands;

    public WatchState WatchState =>
        _watchState;

    public bool IsSubscribed {
        get {
            lock (_subscriptionLock) {
                return _subscriptions.Count > 0;
            }
        }
    }

    public int SubscriptionCount {
        get {
            lock (_subscriptionLock) {
                return _subscriptions.Count;
            }
        }
    }

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
        ArgumentNullException.ThrowIfNull(command);

        if (!IsInTransaction) {
            throw new InvalidOperationException(
                "No transaction is active.");
        }

        _queuedCommands.Add(command);
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

    public void AddSubscription(
        string channel) {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        lock (_subscriptionLock) {
            _subscriptions.Add(channel);
        }
    }

    public bool RemoveSubscription(
        string channel) {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        lock (_subscriptionLock) {
            return _subscriptions.Remove(channel);
        }
    }

    public string[] GetSubscriptions() {
        lock (_subscriptionLock) {
            return [.. _subscriptions];
        }
    }

    public bool TryEnqueue(
        RespValue message) {
        ArgumentNullException.ThrowIfNull(message);

        return _outboundMessages.Writer.TryWrite(message);
    }

    public ChannelReader<RespValue> OutboundMessages =>
        _outboundMessages.Reader;

    public void CompleteOutboundMessages() {
        _outboundMessages.Writer.TryComplete();
    }
}
