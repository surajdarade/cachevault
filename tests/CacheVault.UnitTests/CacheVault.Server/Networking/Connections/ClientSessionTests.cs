using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Networking.Connections;

public sealed class ClientSessionTests {
    [Fact]
    public void NewSession_IsNotInTransaction() {
        var session =
            new ClientSession();

        Assert.False(
            session.IsInTransaction);

        Assert.Empty(
            session.QueuedCommands);
    }

    [Fact]
    public void BeginTransaction_ActivatesTransaction() {
        var session =
            new ClientSession();

        session.BeginTransaction();

        Assert.True(
            session.IsInTransaction);

        Assert.Empty(
            session.QueuedCommands);
    }

    [Fact]
    public void QueueCommand_AddsCommandToTransaction() {
        var session =
            new ClientSession();

        session.BeginTransaction();

        var command =
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]);

        session.QueueCommand(
            command);

        Assert.Single(
            session.QueuedCommands);

        Assert.Equal(
            "SET",
            session.QueuedCommands[0].Name);
    }

    [Fact]
    public void DrainTransaction_ReturnsQueuedCommandsAndEndsTransaction() {
        var session =
            new ClientSession();

        session.BeginTransaction();

        session.QueueCommand(
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]));

        session.QueueCommand(
            new QueuedCommand(
                "GET",
                [
                    new RespBulkString("key")
                ]));

        IReadOnlyList<QueuedCommand> commands =
            session.DrainTransaction();

        Assert.Equal(
            2,
            commands.Count);

        Assert.Equal(
            "SET",
            commands[0].Name);

        Assert.Equal(
            "GET",
            commands[1].Name);

        Assert.False(
            session.IsInTransaction);

        Assert.Empty(
            session.QueuedCommands);
    }

    [Fact]
    public void DiscardTransaction_ClearsQueuedCommandsAndEndsTransaction() {
        var session =
            new ClientSession();

        session.BeginTransaction();

        session.QueueCommand(
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]));

        session.DiscardTransaction();

        Assert.False(
            session.IsInTransaction);

        Assert.Empty(
            session.QueuedCommands);
    }

    [Fact]
    public void BeginTransaction_WhenAlreadyActive_Throws() {
        var session =
            new ClientSession();

        session.BeginTransaction();

        Assert.Throws<InvalidOperationException>(
            session.BeginTransaction);
    }

    [Fact]
    public void QueueCommand_WhenTransactionInactive_Throws() {
        var session =
            new ClientSession();

        var command =
            new QueuedCommand(
                "GET",
                [
                    new RespBulkString("key")
                ]);

        Assert.Throws<InvalidOperationException>(
            () =>
                session.QueueCommand(
                    command));
    }

    [Fact]
    public void DrainTransaction_WhenTransactionInactive_Throws() {
        var session =
            new ClientSession();

        Assert.Throws<InvalidOperationException>(
            session.DrainTransaction);
    }

    [Fact]
    public void DiscardTransaction_WhenTransactionInactive_Throws() {
        var session =
            new ClientSession();

        Assert.Throws<InvalidOperationException>(
            session.DiscardTransaction);
    }

    [Fact]
    public void NewSession_HasEmptyWatchState() {
        var session =
            new ClientSession();

        Assert.False(
            session.WatchState.HasWatchedKeys);

        Assert.Empty(
            session.WatchState.WatchedKeys);
    }

    [Fact]
    public void WatchState_BelongsToSession() {
        var session =
            new ClientSession();

        session.WatchState.Watch(
            "key",
            10);

        Assert.True(
            session.WatchState.HasWatchedKeys);

        Assert.Equal(
            10,
            session.WatchState.WatchedKeys["key"]);
    }

    [Fact]
    public void DiscardTransaction_DoesNotClearWatchState() {
        var session =
            new ClientSession();

        session.WatchState.Watch(
            "key",
            10);

        session.BeginTransaction();

        session.QueueCommand(
            new QueuedCommand(
                "SET",
                []));

        session.DiscardTransaction();

        Assert.False(
            session.IsInTransaction);

        Assert.True(
            session.WatchState.HasWatchedKeys);

        Assert.Equal(
            10,
            session.WatchState.WatchedKeys["key"]);
    }
}