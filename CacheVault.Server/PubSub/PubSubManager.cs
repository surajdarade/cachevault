using System.Collections.Concurrent;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.Server.PubSub;

public sealed class PubSubManager {
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<Guid, ClientSession>> _channels =
        new(StringComparer.Ordinal);

    public void Subscribe(
        ClientSession session,
        string channel) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        ConcurrentDictionary<Guid, ClientSession> subscribers =
            _channels.GetOrAdd(
                channel,
                static _ => new ConcurrentDictionary<Guid, ClientSession>());

        subscribers[session.ConnectionId] = session;
        session.AddSubscription(channel);
    }

    public bool Unsubscribe(
        ClientSession session,
        string channel) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        if (!_channels.TryGetValue(
                channel,
                out ConcurrentDictionary<Guid, ClientSession>? subscribers)) {
            session.RemoveSubscription(channel);
            return false;
        }

        bool removed =
            subscribers.TryRemove(
                session.ConnectionId,
                out _);

        session.RemoveSubscription(channel);

        if (subscribers.IsEmpty) {
            _channels.TryRemove(
                new KeyValuePair<
                    string,
                    ConcurrentDictionary<Guid, ClientSession>>(
                    channel,
                    subscribers));
        }

        return removed;
    }

    public int UnsubscribeAll(
        ClientSession session) {
        ArgumentNullException.ThrowIfNull(session);

        string[] channels =
            session.GetSubscriptions();

        int removedCount = 0;

        foreach (string channel in channels) {
            if (Unsubscribe(session, channel)) {
                removedCount++;
            }
        }

        return removedCount;
    }

    public int Publish(
        string channel,
        string message) {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(message);

        if (!_channels.TryGetValue(
                channel,
                out ConcurrentDictionary<Guid, ClientSession>? subscribers)) {
            return 0;
        }

        RespValue notification =
            new RespArray(
                [
                    new RespBulkString("message"),
                    new RespBulkString(channel),
                    new RespBulkString(message)
                ]);

        int delivered = 0;

        foreach (ClientSession session in subscribers.Values) {
            if (session.TryEnqueue(notification)) {
                delivered++;
            }
        }

        return delivered;
    }

    public void RemoveSession(
        ClientSession session) {
        ArgumentNullException.ThrowIfNull(session);

        foreach (string channel in session.GetSubscriptions()) {
            Unsubscribe(session, channel);
        }
    }
}
