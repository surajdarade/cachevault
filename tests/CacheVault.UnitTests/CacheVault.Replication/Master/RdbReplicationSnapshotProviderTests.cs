using System.Text;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Replication.Master;

namespace CacheVault.UnitTests.CacheVault.Replication.Master;

public sealed class RdbReplicationSnapshotProviderTests {
    [Fact]
    public async Task WriteSnapshotAsync_ShouldWriteRdbSnapshot() {
        var store =
            new TestSnapshotStore(
            [
                new PersistentKeyValue(
                    "key",
                    "value",
                    null)
            ]);

        var provider =
            new RdbReplicationSnapshotProvider(
                store);

        await using var stream =
            new MemoryStream();

        await provider.WriteSnapshotAsync(
            stream);

        byte[] data =
            stream.ToArray();

        Assert.NotEmpty(
            data);

        Assert.Equal(
            "CVDB",
            Encoding.ASCII.GetString(
                data,
                0,
                4));
    }

    [Fact]
    public async Task WriteSnapshotAsync_ShouldFlushDestination() {
        var store =
            new TestSnapshotStore(
            [
                new PersistentKeyValue(
                    "key",
                    "value",
                    null)
            ]);

        var provider =
            new RdbReplicationSnapshotProvider(
                store);

        await using var stream =
            new MemoryStream();

        await provider.WriteSnapshotAsync(
            stream);

        Assert.True(
            stream.Position > 0);
    }

    [Fact]
    public async Task WriteSnapshotAsync_WithNullDestination_ShouldThrow() {
        var store =
            new TestSnapshotStore(
                Array.Empty<PersistentKeyValue>());

        var provider =
            new RdbReplicationSnapshotProvider(
                store);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () =>
                await provider.WriteSnapshotAsync(
                    null!));
    }

    private sealed class TestSnapshotStore :
        IKeyValueStoreSnapshot {
        private readonly IReadOnlyList<PersistentKeyValue>
            _snapshot;

        public TestSnapshotStore(
            IReadOnlyList<PersistentKeyValue> snapshot) {
            _snapshot = snapshot;
        }

        public IReadOnlyList<PersistentKeyValue>
            GetSnapshot() {
            return _snapshot;
        }
    }
}