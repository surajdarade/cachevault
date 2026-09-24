using CacheVault.Core.Lists;

namespace CacheVault.UnitTests.CacheVault.Core.Lists;

public sealed class InMemoryListStoreTests {
    [Fact]
    public void PushLeft_PreservesRedisOrder() {
        var store = new InMemoryListStore();

        int length = store.PushLeft(
            "numbers",
            ["one", "two", "three"]);

        Assert.Equal(3, length);
        Assert.Equal(
            ["three", "two", "one"],
            store.Range("numbers", 0, -1));
    }

    [Fact]
    public void PushRight_AppendsValuesInOrder() {
        var store = new InMemoryListStore();

        store.PushRight("numbers", ["one", "two"]);
        store.PushRight("numbers", ["three", "four"]);

        Assert.Equal(
            ["one", "two", "three", "four"],
            store.Range("numbers", 0, -1));
    }

    [Fact]
    public void Pop_RemovesFromExpectedSide() {
        var store = new InMemoryListStore();
        store.PushRight("numbers", ["one", "two", "three"]);

        Assert.Equal("one", store.PopLeft("numbers"));
        Assert.Equal("three", store.PopRight("numbers"));
        Assert.Equal(["two"], store.Range("numbers", 0, -1));
    }

    [Fact]
    public void PopLastElement_RemovesList() {
        var store = new InMemoryListStore();
        store.PushRight("numbers", ["one"]);

        Assert.Equal("one", store.PopLeft("numbers"));
        Assert.False(store.Contains("numbers"));
        Assert.Null(store.PopRight("numbers"));
    }

    [Fact]
    public void Range_SupportsNegativeIndexes() {
        var store = new InMemoryListStore();
        store.PushRight("numbers", ["zero", "one", "two", "three"]);

        Assert.Equal(
            ["one", "two"],
            store.Range("numbers", 1, -2));
    }

    [Fact]
    public void Range_OutOfBoundsIsClamped() {
        var store = new InMemoryListStore();
        store.PushRight("numbers", ["zero", "one"]);

        Assert.Equal(
            ["zero", "one"],
            store.Range("numbers", -100, 100));
        Assert.Empty(store.Range("numbers", 2, 3));
    }
}
