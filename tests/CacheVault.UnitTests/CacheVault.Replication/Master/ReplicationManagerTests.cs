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

    [Fact]
    public async Task ReplicateAsync_ConcurrentCalls_ShouldProduceContiguousOffsets() {
        var fixture =
            CreateFixture();

        const int commandCount = 100;

        Task[] tasks =
            Enumerable.Range(
                    0,
                    commandCount)
                .Select(
                    index =>
                        fixture.Manager.ReplicateAsync(
                            "SET",
                            [
                                $"key-{index}",
                                $"value-{index}"
                            ]).AsTask())
                .ToArray();

        await Task.WhenAll(
            tasks);

        Assert.Equal(
            commandCount,
            fixture.Broadcaster.BroadcastedEntries.Count);

        Assert.Equal(
            fixture.Manager.ReplicationOffset,
            fixture.Backlog.EndOffset);

        Assert.Equal(
            fixture.Manager.ReplicationOffset,
            fixture.Broadcaster.BroadcastedEntries
                .Max(
                    entry =>
                        entry.EndOffset));

        var orderedEntries =
            fixture.Broadcaster.BroadcastedEntries
                .OrderBy(
                    entry =>
                        entry.StartOffset)
                .ToArray();

        long expectedOffset = 0;

        foreach (ReplicationEntry entry in orderedEntries)
        {
            Assert.Equal(
                expectedOffset,
                entry.StartOffset);

            Assert.Equal(
                entry.Length,
                entry.EndOffset - entry.StartOffset);

            expectedOffset =
                entry.EndOffset;
        }

        Assert.Equal(
            fixture.Manager.ReplicationOffset,
            expectedOffset);
    }

    [Fact]
    public async Task ReplicateAsync_CancelledWhileWaitingForPublication_ShouldNotPublish() {
        var fixture =
            CreateFixture();

        var blockingBroadcaster =
            new BlockingReplicationBroadcaster();

        var manager =
            new ReplicationManager(
                fixture.State,
                CreateEncoder(),
                fixture.Backlog,
                blockingBroadcaster);

        Task firstReplication =
            manager.ReplicateAsync(
                "SET",
                ["first", "one"])
            .AsTask();

        await blockingBroadcaster
            .FirstBroadcastStarted
            .WaitAsync(TimeSpan.FromSeconds(5));

        long offsetAfterFirstCommand =
            manager.ReplicationOffset;

        int broadcastCountAfterFirstCommand =
            blockingBroadcaster
                .BroadcastedEntries
                .Count;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task secondReplication =
            manager.ReplicateAsync(
                "SET",
                ["second", "two"],
                cancellationTokenSource.Token)
            .AsTask();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await secondReplication);

        Assert.Equal(
            offsetAfterFirstCommand,
            manager.ReplicationOffset);

        Assert.Equal(
            broadcastCountAfterFirstCommand,
            blockingBroadcaster
                .BroadcastedEntries
                .Count);

        Assert.Equal(
            offsetAfterFirstCommand,
            fixture.Backlog.EndOffset);

        blockingBroadcaster.ReleaseFirstBroadcast();

        await firstReplication.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Single(
            blockingBroadcaster.BroadcastedEntries);

        string broadcastData =
            Encoding.UTF8.GetString(
                blockingBroadcaster
                    .BroadcastedEntries[0]
                    .Data);

        Assert.Contains(
            "first",
            broadcastData);

        Assert.DoesNotContain(
            "second",
            broadcastData);
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

    private sealed class BlockingReplicationBroadcaster :
    IReplicationBroadcaster {
        private readonly TaskCompletionSource<bool>
            _firstBroadcastStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _releaseFirstBroadcast =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly List<ReplicationEntry>
            _broadcastedEntries = [];

        private int _broadcastCount;

        public Task FirstBroadcastStarted =>
            _firstBroadcastStarted.Task;

        public IReadOnlyList<ReplicationEntry>
            BroadcastedEntries =>
            _broadcastedEntries;

        public async ValueTask BroadcastAsync(
            ReplicationEntry entry,
            CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entry);

            int broadcastNumber =
                Interlocked.Increment(
                    ref _broadcastCount);

            _broadcastedEntries.Add(
                entry);

            if (broadcastNumber == 1) {
                _firstBroadcastStarted.TrySetResult(
                    true);

                await _releaseFirstBroadcast.Task;

                cancellationToken.ThrowIfCancellationRequested();

                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public void ReleaseFirstBroadcast() {
            _releaseFirstBroadcast.TrySetResult(
                true);
        }
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