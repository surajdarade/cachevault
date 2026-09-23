using CacheVault.Replication.State;

namespace CacheVault.UnitTests.CacheVault.Replication.State;

public sealed class ReplicationEntryTests {
    [Fact]
    public void Constructor_ShouldCreateEntry() {
        byte[] data =
            [1, 2, 3, 4, 5];

        var entry =
            new ReplicationEntry(
                10,
                15,
                data);

        Assert.Equal(
            10,
            entry.StartOffset);

        Assert.Equal(
            15,
            entry.EndOffset);

        Assert.Equal(
            5,
            entry.Length);

        Assert.Equal(
            data,
            entry.Data);
    }

    [Fact]
    public void Length_ShouldEqualOffsetDifference() {
        var entry =
            new ReplicationEntry(
                100,
                150,
                new byte[50]);

        Assert.Equal(
            50,
            entry.Length);
    }

    [Fact]
    public void Constructor_WithNegativeStartOffset_ShouldThrow() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ReplicationEntry(
                    -1,
                    10,
                    new byte[11]));
    }

    [Fact]
    public void Constructor_WithEndOffsetEqualToStartOffset_ShouldThrow() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ReplicationEntry(
                    10,
                    10,
                    Array.Empty<byte>()));
    }

    [Fact]
    public void Constructor_WithEndOffsetBeforeStartOffset_ShouldThrow() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ReplicationEntry(
                    20,
                    10,
                    Array.Empty<byte>()));
    }

    [Fact]
    public void Constructor_WithNullData_ShouldThrow() {
        Assert.Throws<ArgumentNullException>(
            () =>
                new ReplicationEntry(
                    0,
                    1,
                    null!));
    }

    [Fact]
    public void Constructor_WithIncorrectDataLength_ShouldThrow() {
        Assert.Throws<ArgumentException>(
            () =>
                new ReplicationEntry(
                    10,
                    20,
                    new byte[5]));
    }

    [Fact]
    public void Constructor_WithDataLongerThanOffsetRange_ShouldThrow() {
        Assert.Throws<ArgumentException>(
            () =>
                new ReplicationEntry(
                    10,
                    15,
                    new byte[10]));
    }

    [Fact]
    public void Constructor_WithZeroStartOffset_ShouldBeValid() {
        var entry =
            new ReplicationEntry(
                0,
                4,
                new byte[4]);

        Assert.Equal(
            0,
            entry.StartOffset);

        Assert.Equal(
            4,
            entry.EndOffset);

        Assert.Equal(
            4,
            entry.Length);
    }

    [Fact]
    public void Data_ShouldPreserveBinaryPayload() {
        byte[] data =
        [
            0,
            1,
            127,
            128,
            254,
            255
        ];

        var entry =
            new ReplicationEntry(
                50,
                56,
                data);

        Assert.Equal(
            data,
            entry.Data);
    }
}