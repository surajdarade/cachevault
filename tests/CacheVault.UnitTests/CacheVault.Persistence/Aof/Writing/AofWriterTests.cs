using System.Text;
using System.Threading.Tasks;
using CacheVault.Persistence.Aof.Writing;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Persistence.Aof.Reading;

namespace CacheVault.UnitTests.Persistence.Aof.Writing;

public sealed class AofWriterTests {
    [Fact]
    public async Task AppendCommandAsync_ShouldAppendCommand() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            await writer.AppendCommandAsync(
                "SET",
                ["key", "value"]);

            await writer.DisposeAsync();

            byte[] data =
                await File.ReadAllBytesAsync(
                    filePath);

            var parser =
                new RespParser();

            RespValue value =
                parser.Parse(data);

            var command =
                Assert.IsType<RespArray>(
                    value);

            Assert.NotNull(command.Values);
            Assert.Equal(
                3,
                command.Values.Count);

            Assert.Equal(
                "SET",
                Assert.IsType<RespBulkString>(
                    command.Values[0]).Value);

            Assert.Equal(
                "key",
                Assert.IsType<RespBulkString>(
                    command.Values[1]).Value);

            Assert.Equal(
                "value",
                Assert.IsType<RespBulkString>(
                    command.Values[2]).Value);
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task AppendCommandAsync_ShouldAppendMultipleCommands() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            await writer.AppendCommandAsync(
                "SET",
                ["key", "value"]);

            await writer.AppendCommandAsync(
                "DEL",
                ["key"]);

            await writer.DisposeAsync();

            byte[] data =
                await File.ReadAllBytesAsync(
                    filePath);

            var reader =
                new AofCommandReader(
                    new RespParser());

            using var stream =
                new MemoryStream(data);

            RespArray first =
                reader.ReadCommand(stream);

            RespArray second =
                reader.ReadCommand(stream);

            Assert.Equal(
                "SET",
                Assert.IsType<RespBulkString>(
                    first.Values![0]).Value);

            Assert.Equal(
                "DEL",
                Assert.IsType<RespBulkString>(
                    second.Values![0]).Value);
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task AppendCommandAsync_ShouldAppendToExistingAof() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            var serializer =
                new RespSerializer();

            var firstWriter =
                new AofWriter(
                    filePath,
                    serializer);

            await firstWriter.AppendCommandAsync(
                "SET",
                ["key", "first"]);

            await firstWriter.DisposeAsync();

            var secondWriter =
                new AofWriter(
                    filePath,
                    serializer);

            await secondWriter.AppendCommandAsync(
                "SET",
                ["key", "second"]);

            await secondWriter.DisposeAsync();

            byte[] data =
                await File.ReadAllBytesAsync(
                    filePath);

            using var stream =
                new MemoryStream(data);

            var reader =
                new AofCommandReader(
                    new RespParser());

            RespArray first =
                reader.ReadCommand(stream);

            RespArray second =
                reader.ReadCommand(stream);

            Assert.Equal(
                "first",
                Assert.IsType<RespBulkString>(
                    first.Values![2]).Value);

            Assert.Equal(
                "second",
                Assert.IsType<RespBulkString>(
                    second.Values![2]).Value);
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task AppendCommandAsync_ShouldHandleConcurrentWrites() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            const int commandCount = 100;

            Task[] tasks =
                Enumerable
                    .Range(0, commandCount)
                    .Select(
                        index =>
                            writer.AppendCommandAsync(
                                "SET",
                                [
                                    $"key-{index}",
                                    $"value-{index}"
                                ]).AsTask())
                    .ToArray();

            await Task.WhenAll(tasks);

            await writer.DisposeAsync();

            byte[] data =
                await File.ReadAllBytesAsync(
                    filePath);

            using var stream =
                new MemoryStream(data);

            var reader =
                new AofCommandReader(
                    new RespParser());

            for (int i = 0; i < commandCount; i++) {
                RespArray command =
                    reader.ReadCommand(stream);

                Assert.Equal(
                    "SET",
                    Assert.IsType<RespBulkString>(
                        command.Values![0]).Value);

                Assert.Equal(
                    3,
                    command.Values.Count);
            }
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task AppendCommandAsync_ShouldRejectNullCommandName() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            await using var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                    await writer.AppendCommandAsync(
                        null!,
                        []));
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task AppendCommandAsync_ShouldRespectCancellation() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            await using var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(
                async () =>
                    await writer.AppendCommandAsync(
                        "SET",
                        ["key", "value"],
                        cancellationTokenSource.Token));
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task AppendCommandAsync_ShouldRejectNullArguments() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                    await writer.AppendCommandAsync(
                        "SET",
                        null!));

            await writer.DisposeAsync();
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent() {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.aof");

        try {
            var writer =
                new AofWriter(
                    filePath,
                    new RespSerializer());

            await writer.AppendCommandAsync(
                "PING",
                []);

            await writer.DisposeAsync();
            await writer.DisposeAsync();
        }
        finally {
            DeleteIfExists(filePath);
        }
    }

    private static void DeleteIfExists(
        string filePath) {
        if (File.Exists(filePath)) {
            File.Delete(filePath);
        }
    }
}