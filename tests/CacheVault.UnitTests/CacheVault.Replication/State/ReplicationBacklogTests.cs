using System.Text;
using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.State;

public sealed class ReplicationBacklogTests {
    [Fact]
    public void Constructor_ShouldInitializeEmptyBacklog() {
        var backlog =
            new ReplicationBacklog(
                100);

        Assert.Equal(
            100,
            backlog.Capacity);

        Assert.Equal(
            0,
            backlog.Length);

        Assert.Equal(
            0,
            backlog.FirstOffset);

        Assert.Equal(
            0,
            backlog.EndOffset);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ShouldThrow() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ReplicationBacklog(
                    0));
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ShouldThrow() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ReplicationBacklog(
                    -1));
    }

    [Fact]
    public void Append_ShouldStoreEntry() {
        var backlog =
            new ReplicationBacklog(
                100);

        ReplicationEntry entry =
            CreateEntry(
                0,
                "hello");

        backlog.Append(
            entry);

        Assert.Equal(
            5,
            backlog.Length);

        Assert.Equal(
            0,
            backlog.FirstOffset);

        Assert.Equal(
            5,
            backlog.EndOffset);
    }

    [Fact]
    public void TryReadFrom_ShouldReturnData() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                0,
                "hello"));

        bool found =
            backlog.TryReadFrom(
                0,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "hello",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void TryReadFrom_FromMiddle_ShouldReturnRemainingData() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                0,
                "hello"));

        bool found =
            backlog.TryReadFrom(
                2,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "llo",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void TryReadFrom_AtEndOffset_ShouldReturnEmptyData() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                0,
                "hello"));

        bool found =
            backlog.TryReadFrom(
                5,
                out byte[] data);

        Assert.True(
            found);

        Assert.Empty(
            data);
    }

    [Fact]
    public void TryReadFrom_BeforeFirstOffset_ShouldReturnFalse() {
        var backlog =
            new ReplicationBacklog(
                5);

        backlog.Append(
            CreateEntry(
                100,
                "hello"));

        bool found =
            backlog.TryReadFrom(
                99,
                out byte[] data);

        Assert.False(
            found);

        Assert.Empty(
            data);
    }

    [Fact]
    public void TryReadFrom_AfterEndOffset_ShouldReturnFalse() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                0,
                "hello"));

        bool found =
            backlog.TryReadFrom(
                6,
                out byte[] data);

        Assert.False(
            found);

        Assert.Empty(
            data);
    }

    [Fact]
    public void Append_ShouldRequireContiguousOffsets() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                0,
                "hello"));

        Assert.Throws<InvalidOperationException>(
            () =>
                backlog.Append(
                    CreateEntry(
                        10,
                        "world")));
    }

    [Fact]
    public void Append_ShouldAllowEntryStartingAtNonZeroOffset() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                500,
                "hello"));

        Assert.Equal(
            500,
            backlog.FirstOffset);

        Assert.Equal(
            505,
            backlog.EndOffset);

        bool found =
            backlog.TryReadFrom(
                500,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "hello",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void Append_ShouldSupportMultipleEntries() {
        var backlog =
            new ReplicationBacklog(
                100);

        backlog.Append(
            CreateEntry(
                0,
                "hello"));

        backlog.Append(
            CreateEntry(
                5,
                "world"));

        bool found =
            backlog.TryReadFrom(
                0,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "helloworld",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void Append_WhenCapacityIsExceeded_ShouldDiscardOldestBytes() {
        var backlog =
            new ReplicationBacklog(
                10);

        backlog.Append(
            CreateEntry(
                0,
                "12345"));

        backlog.Append(
            CreateEntry(
                5,
                "67890"));

        backlog.Append(
            CreateEntry(
                10,
                "ABCDE"));

        Assert.Equal(
            10,
            backlog.Length);

        Assert.Equal(
            5,
            backlog.FirstOffset);

        Assert.Equal(
            15,
            backlog.EndOffset);

        bool found =
            backlog.TryReadFrom(
                5,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "67890ABCDE",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void Append_WhenSingleEntryExceedsCapacity_ShouldKeepNewestBytes() {
        var backlog =
            new ReplicationBacklog(
                5);

        backlog.Append(
            CreateEntry(
                100,
                "123456789"));

        Assert.Equal(
            5,
            backlog.Length);

        Assert.Equal(
            104,
            backlog.FirstOffset);

        Assert.Equal(
            109,
            backlog.EndOffset);

        bool found =
            backlog.TryReadFrom(
                104,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "56789",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void TryReadFrom_ShouldHandleCircularWrapAround() {
        var backlog =
            new ReplicationBacklog(
                10);

        backlog.Append(
            CreateEntry(
                0,
                "12345678"));

        backlog.Append(
            CreateEntry(
                8,
                "ABCDEFG"));

        bool found =
            backlog.TryReadFrom(
                5,
                out byte[] data);

        Assert.True(
            found);

        Assert.Equal(
            "678ABCDEFG",
            Encoding.UTF8.GetString(
                data));
    }

    [Fact]
    public void Append_ShouldUpdateOffsetsAfterEviction() {
        var backlog =
            new ReplicationBacklog(
                5);

        backlog.Append(
            CreateEntry(
                0,
                "abc"));

        backlog.Append(
            CreateEntry(
                3,
                "def"));

        Assert.Equal(
            1,
            backlog.FirstOffset);

        Assert.Equal(
            6,
            backlog.EndOffset);

        Assert.Equal(
            5,
            backlog.Length);
    }

    private static ReplicationEntry CreateEntry(
        long startOffset,
        string value) {
        byte[] data =
            Encoding.UTF8.GetBytes(
                value);

        return new ReplicationEntry(
            startOffset,
            startOffset + data.Length,
            data);
    }
}