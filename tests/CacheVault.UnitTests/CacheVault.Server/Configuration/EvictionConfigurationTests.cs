using CacheVault.Core.Models;
using CacheVault.Server.Configuration;

namespace CacheVault.UnitTests.CacheVault.Server.Configuration;

public sealed class EvictionConfigurationTests {
    [Theory]
    [InlineData("noeviction", EvictionPolicy.NoEviction)]
    [InlineData("lru", EvictionPolicy.Lru)]
    [InlineData("allkeys-lru", EvictionPolicy.Lru)]
    [InlineData("lfu", EvictionPolicy.Lfu)]
    [InlineData("allkeys-lfu", EvictionPolicy.Lfu)]
    [InlineData("random", EvictionPolicy.Random)]
    [InlineData("allkeys-random", EvictionPolicy.Random)]
    public void ParseEvictionPolicy_ReturnsExpectedPolicy(
        string value,
        EvictionPolicy expected) {
        Assert.Equal(
            expected,
            EvictionPolicyParser.Parse(value));
    }

    [Fact]
    public void Apply_WithMaxMemoryAndPolicy_ConfiguresOptions() {
        var options =
            new ServerOptions();

        CommandLineOptions.Apply(
            options,
            [
                "--maxmemory",
                "1024",
                "--eviction-policy",
                "lru"
            ]);

        Assert.Equal(
            1024,
            options.MaxMemoryBytes);

        Assert.Equal(
            EvictionPolicy.Lru,
            options.EvictionPolicy);
    }

    [Fact]
    public void Apply_WithNegativeMaxMemory_Throws() {
        var options =
            new ServerOptions();

        Assert.Throws<ArgumentException>(
            () =>
                CommandLineOptions.Apply(
                    options,
                    [
                        "--maxmemory",
                        "-1"
                    ]));
    }

    [Fact]
    public void Apply_WithUnknownPolicy_Throws() {
        var options =
            new ServerOptions();

        Assert.Throws<ArgumentException>(
            () =>
                CommandLineOptions.Apply(
                    options,
                    [
                        "--eviction-policy",
                        "unknown"
                    ]));
    }
}
