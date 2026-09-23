using System.Text;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Replication.Abstractions;
using CacheVault.Replication.Master;
using CacheVault.Replication.Protocol;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class ReplicationManagerTests {
    [Fact]
    public async Task ReplicateAsync_ShouldAppendCommandToBacklog() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["name", "Suraj"]);

        Assert.True(
            fixture.Backlog.TryReadFrom(
                0,
                out byte[] data));

        string command =
            Encoding.UTF8.GetString(
                data);

        Assert.Contains(
            "SET",
            command);

        Assert.Contains(
            "name",
            command);

        Assert.Contains(
            "Suraj",
            command);
    }

    [Fact]
    public async Task ReplicateAsync_ShouldAdvanceReplicationOffset() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["key", "value"]);

        Assert.Equal(
            fixture.Backlog.EndOffset,
            fixture.Manager.ReplicationOffset);

        Assert.Equal(
            fixture.Backlog.Length,
            fixture.Manager.ReplicationOffset);
    }

    [Fact]
    public async Task ReplicateAsync_MultipleCommands_ShouldUseContiguousOffsets() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["first", "one"]);

        long firstOffset =
            fixture.Manager.ReplicationOffset;

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["second", "two"]);

        long secondOffset =
            fixture.Manager.ReplicationOffset;

        Assert.True(
            secondOffset > firstOffset);

        Assert.Equal(
            secondOffset,
            fixture.Backlog.EndOffset);

        Assert.Equal(
            secondOffset,
            fixture.Backlog.Length);
    }

    [Fact]
    public async Task ReplicateAsync_ShouldStartFirstCommandAtOffsetZero() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "DEL",
            ["key"]);

        Assert.Equal(
            0,
            fixture.Backlog.FirstOffset);

        Assert.True(
            fixture.Manager.ReplicationOffset > 0);
    }

    [Fact]
    public async Task ReplicateAsync_ShouldPreserveCommandOrder() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["first", "one"]);

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["second", "two"]);

        await fixture.Manager.ReplicateAsync(
            "DEL",
            ["first"]);

        Assert.True(
            fixture.Backlog.TryReadFrom(
                0,
                out byte[] data));

        string result =
            Encoding.UTF8.GetString(
                data);

        int firstIndex =
            result.IndexOf(
                "first",
                StringComparison.Ordinal);

        int secondIndex =
            result.IndexOf(
                "second",
                StringComparison.Ordinal);

        int deleteIndex =
            result.LastIndexOf(
                "DEL",
                StringComparison.Ordinal);

        Assert.True(
            firstIndex >= 0);

        Assert.True(
            secondIndex > firstIndex);

        Assert.True(
            deleteIndex > secondIndex);
    }

    [Fact]
    public async Task ReplicateAsync_ShouldBroadcastCreatedEntry() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["key", "value"]);

        Assert.Single(
            fixture.Broadcaster.BroadcastedEntries);

        ReplicationEntry entry =
            fixture.Broadcaster.BroadcastedEntries[0];

        Assert.Equal(
            0,
            entry.StartOffset);

        Assert.Equal(
            entry.Length,
            entry.EndOffset);

        Assert.Equal(
            entry.EndOffset,
            fixture.Manager.ReplicationOffset);

        Assert.True(
            fixture.Backlog.TryReadFrom(
                0,
                out byte[] backlogData));

        Assert.Equal(
            entry.Data,
            backlogData);
    }

    [Fact]
    public async Task ReplicateAsync_MultipleCommands_ShouldBroadcastEachEntry() {
        var fixture =
            CreateFixture();

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["first", "one"]);

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["second", "two"]);

        await fixture.Manager.ReplicateAsync(
            "DEL",
            ["first"]);

        Assert.Equal(
            3,
            fixture.Broadcaster.BroadcastedEntries.Count);

        Assert.Equal(
            0,
            fixture.Broadcaster.BroadcastedEntries[0].StartOffset);

        Assert.Equal(
            fixture.Broadcaster.BroadcastedEntries[0].EndOffset,
            fixture.Broadcaster.BroadcastedEntries[1].StartOffset);

        Assert.Equal(
            fixture.Broadcaster.BroadcastedEntries[1].EndOffset,
            fixture.Broadcaster.BroadcastedEntries[2].StartOffset);

        Assert.Equal(
            fixture.Manager.ReplicationOffset,
            fixture.Broadcaster.BroadcastedEntries[2].EndOffset);
    }

    [Fact]
    public async Task ReplicateAsync_WithNullCommandName_ShouldThrow() {
        var fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () =>
                await fixture.Manager.ReplicateAsync(
                    null!,
                    ["key", "value"]));
    }

    [Fact]
    public async Task ReplicateAsync_WithEmptyCommandName_ShouldThrow() {
        var fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await fixture.Manager.ReplicateAsync(
                    string.Empty,
                    ["key", "value"]));
    }

    [Fact]
    public async Task ReplicateAsync_WithNullArguments_ShouldThrow() {
        var fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () =>
                await fixture.Manager.ReplicateAsync(
                    "SET",
                    null!));
    }

    [Fact]
    public async Task ReplicateAsync_ShouldNotChangeOffsetWhenEncodingFails() {
        var fixture =
            CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () =>
                await fixture.Manager.ReplicateAsync(
                    "SET",
                    ["key", null!]));

        Assert.Equal(
            0,
            fixture.Manager.ReplicationOffset);

        Assert.Equal(
            0,
            fixture.Backlog.Length);

        Assert.Empty(
            fixture.Broadcaster.BroadcastedEntries);
    }

    [Fact]
    public async Task ReplicateAsync_ShouldExposeCurrentReplicationOffset() {
        var fixture =
            CreateFixture();

        Assert.Equal(
            0,
            fixture.Manager.ReplicationOffset);

        await fixture.Manager.ReplicateAsync(
            "SET",
            ["key", "value"]);

        Assert.Equal(
            fixture.State.ReplicationOffset,
            fixture.Manager.ReplicationOffset);
    }

    [Fact]
    public async Task ReplicateAsync_WhenBroadcastFails_ShouldPropagateException() {
        var broadcaster =
            new FailingReplicationBroadcaster();

        var state =
            new ReplicationState();

        var backlog =
            new ReplicationBacklog(
                4096);

        var manager =
            new ReplicationManager(
                state,
                CreateEncoder(),
                backlog,
                broadcaster);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await manager.ReplicateAsync(
                    "SET",
                    ["key", "value"]));

        Assert.True(
            manager.ReplicationOffset > 0);

        Assert.Equal(
            manager.ReplicationOffset,
            backlog.EndOffset);

        Assert.True(
            backlog.Length > 0);
    }

    [Fact]
    public async Task ReplicateAsync_WhenCancelledBeforeReplication_ShouldThrow() {
        var fixture =
            CreateFixture();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await fixture.Manager.ReplicateAsync(
                    "SET",
                    ["key", "value"],
                    cancellationTokenSource.Token));

        Assert.Equal(
            0,
            fixture.Manager.ReplicationOffset);

        Assert.Equal(
            0,
            fixture.Backlog.Length);

        Assert.Empty(
            fixture.Broadcaster.BroadcastedEntries);
    }

    private static TestFixture CreateFixture() {
        var state =
            new ReplicationState();

        var encoder =
            CreateEncoder();

        var backlog =
            new ReplicationBacklog(
                4096);

        var broadcaster =
            new TestReplicationBroadcaster();

        var manager =
            new ReplicationManager(
                state,
                encoder,
                backlog,
                broadcaster);

        return new TestFixture(
            manager,
            state,
            backlog,
            broadcaster);
    }

    private static RespReplicationCommandEncoder CreateEncoder() {
        return new RespReplicationCommandEncoder(
            new RespSerializer());
    }

    private sealed class TestFixture {
        public TestFixture(
            ReplicationManager manager,
            ReplicationState state,
            ReplicationBacklog backlog,
            TestReplicationBroadcaster broadcaster) {
            Manager =
                manager;

            State =
                state;

            Backlog =
                backlog;

            Broadcaster =
                broadcaster;
        }

        public ReplicationManager Manager { get; }

        public ReplicationState State { get; }

        public ReplicationBacklog Backlog { get; }

        public TestReplicationBroadcaster Broadcaster { get; }
    }

    private sealed class TestReplicationBroadcaster :
        IReplicationBroadcaster {
        public List<ReplicationEntry> BroadcastedEntries { get; } =
            [];

        public ValueTask BroadcastAsync(
            ReplicationEntry entry,
            CancellationToken cancellationToken = default) {
            BroadcastedEntries.Add(
                entry);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingReplicationBroadcaster :
        IReplicationBroadcaster {
        public ValueTask BroadcastAsync(
            ReplicationEntry entry,
            CancellationToken cancellationToken = default) {
            throw new InvalidOperationException(
                "Replication broadcast failed.");
        }
    }
}