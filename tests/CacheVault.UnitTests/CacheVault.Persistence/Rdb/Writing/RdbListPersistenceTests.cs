using CacheVault.Core.Abstractions;
using CacheVault.Core.Lists;
using CacheVault.Core.Storage;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Persistence.Rdb.Writing;
using CacheVault.Persistence.Recovery;

namespace CacheVault.UnitTests.CacheVault.Persistence.Rdb.Writing;

public sealed class RdbListPersistenceTests
{
    [Fact]
    public void Snapshot_RoundTripsLists()
    {
        var store =
            new InMemoryKeyValueStore(
                new TestClock());

        var lists =
            new InMemoryListStore();

        lists.PushRight(
            "numbers",
            ["one", "two", "three"]);

        using var stream =
            new MemoryStream();

        var writer =
            new RdbSnapshotWriter(
                store,
                lists);

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        writer.Write(
            stream,
            now);

        stream.Position = 0;

        var restoredStore =
            new InMemoryKeyValueStore(
                new TestClock());

        var restoredLists =
            new InMemoryListStore();

        var loader =
            new RdbSnapshotLoader(
                restoredStore,
                new RdbSnapshotReader(),
                restoredLists);

        int restored =
            loader.Load(
                stream,
                now);

        Assert.Equal(1, restored);
        Assert.Equal(
            ["one", "two", "three"],
            restoredLists.Range(
                "numbers",
                0,
                -1));
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow =>
            DateTimeOffset.UtcNow;
    }
}
