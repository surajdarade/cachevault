using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Networking.Connections;

public sealed class WatchStateTests {
    [Fact]
    public void NewState_HasNoWatchedKeys() {
        var state =
            new WatchState();

        Assert.False(
            state.HasWatchedKeys);

        Assert.Empty(
            state.WatchedKeys);
    }

    [Fact]
    public void Watch_AddsKeyAndVersion() {
        var state =
            new WatchState();

        state.Watch(
            "key",
            10);

        Assert.True(
            state.HasWatchedKeys);

        Assert.Single(
            state.WatchedKeys);

        Assert.Equal(
            10,
            state.WatchedKeys["key"]);
    }

    [Fact]
    public void Watch_SameKey_ReplacesObservedVersion() {
        var state =
            new WatchState();

        state.Watch(
            "key",
            10);

        state.Watch(
            "key",
            20);

        Assert.Single(
            state.WatchedKeys);

        Assert.Equal(
            20,
            state.WatchedKeys["key"]);
    }

    [Fact]
    public void Watch_MultipleKeys_TracksEachVersion() {
        var state =
            new WatchState();

        state.Watch(
            "first",
            10);

        state.Watch(
            "second",
            20);

        state.Watch(
            "third",
            30);

        Assert.Equal(
            3,
            state.WatchedKeys.Count);

        Assert.Equal(
            10,
            state.WatchedKeys["first"]);

        Assert.Equal(
            20,
            state.WatchedKeys["second"]);

        Assert.Equal(
            30,
            state.WatchedKeys["third"]);
    }

    [Fact]
    public void HasChanged_WhenVersionIsUnchanged_ReturnsFalse() {
        var state =
            new WatchState();

        state.Watch(
            "key",
            10);

        long VersionProvider(
            string key) {
            return 10;
        }

        Assert.False(
            state.HasChanged(
                VersionProvider));
    }

    [Fact]
    public void HasChanged_WhenVersionChanges_ReturnsTrue() {
        var state =
            new WatchState();

        state.Watch(
            "key",
            10);

        long VersionProvider(
            string key) {
            return 11;
        }

        Assert.True(
            state.HasChanged(
                VersionProvider));
    }

    [Fact]
    public void HasChanged_WhenOneOfMultipleKeysChanges_ReturnsTrue() {
        var state =
            new WatchState();

        state.Watch(
            "first",
            10);

        state.Watch(
            "second",
            20);

        long VersionProvider(
            string key) {
            return key == "first"
                ? 10
                : 21;
        }

        Assert.True(
            state.HasChanged(
                VersionProvider));
    }

    [Fact]
    public void HasChanged_WhenAllVersionsMatch_ReturnsFalse() {
        var state =
            new WatchState();

        state.Watch(
            "first",
            10);

        state.Watch(
            "second",
            20);

        long VersionProvider(
            string key) {
            return key == "first"
                ? 10
                : 20;
        }

        Assert.False(
            state.HasChanged(
                VersionProvider));
    }

    [Fact]
    public void Clear_RemovesAllWatchedKeys() {
        var state =
            new WatchState();

        state.Watch(
            "first",
            10);

        state.Watch(
            "second",
            20);

        state.Clear();

        Assert.False(
            state.HasWatchedKeys);

        Assert.Empty(
            state.WatchedKeys);
    }

    [Fact]
    public void Watch_NullKey_Throws() {
        var state =
            new WatchState();

        Assert.Throws<ArgumentNullException>(
            () =>
                state.Watch(
                    null!,
                    1));
    }

    [Fact]
    public void Watch_EmptyKey_Throws() {
        var state =
            new WatchState();

        Assert.Throws<ArgumentException>(
            () =>
                state.Watch(
                    "",
                    1));
    }

    [Fact]
    public void HasChanged_NullVersionProvider_Throws() {
        var state =
            new WatchState();

        Assert.Throws<ArgumentNullException>(
            () =>
                state.HasChanged(
                    null!));
    }
}