
using System.Text;
using CacheVault.Protocol.Exceptions;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Connections;

public sealed class RespStreamReaderTests {
    [Fact]
    public async Task ReadAsync_CompleteMessage_ReturnsValue() {
        byte[] data =
            Encoding.UTF8.GetBytes(
                "*1\r\n$4\r\nPING\r\n");

        using var stream =
            new MemoryStream(data);

        var reader =
            new RespStreamReader(
                new RespParser());

        RespValue? result =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        AssertPingCommand(result);
    }

    [Fact]
    public async Task ReadAsync_MultipleMessages_ReadsOneAtATime() {
        byte[] data =
            Encoding.UTF8.GetBytes(
                "*1\r\n$4\r\nPING\r\n" +
                "*1\r\n$4\r\nPONG\r\n");

        using var stream =
            new MemoryStream(data);

        var reader =
            new RespStreamReader(
                new RespParser());

        RespValue? first =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        RespValue? second =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        AssertPingCommand(first);

        AssertPongCommand(second);
    }

    [Fact]
    public async Task ReadAsync_PartialMessageAcrossReads_ReturnsValue() {
        byte[] firstPart =
            Encoding.UTF8.GetBytes(
                "*1\r\n$4\r\nPI");

        byte[] secondPart =
            Encoding.UTF8.GetBytes(
                "NG\r\n");

        using var stream =
            new ChunkedReadStream(
            [
                firstPart,
                secondPart
            ]);

        var reader =
            new RespStreamReader(
                new RespParser());

        RespValue? result =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        AssertPingCommand(result);
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ReturnsNull() {
        using var stream =
            new MemoryStream();

        var reader =
            new RespStreamReader(
                new RespParser());

        RespValue? result =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadAsync_IncompleteMessageAtEof_Throws() {
        byte[] data =
            Encoding.UTF8.GetBytes(
                "*1\r\n$4\r\nPIN");

        using var stream =
            new MemoryStream(data);

        var reader =
            new RespStreamReader(
                new RespParser());

        await Assert.ThrowsAsync<RespIncompleteException>(
            async () =>
            {
                await reader.ReadAsync(
                    stream,
                    CancellationToken.None);
            });
    }

    [Fact]
    public async Task ReadAsync_MalformedMessage_ThrowsFormatException() {
        byte[] data =
            Encoding.UTF8.GetBytes(
                "?invalid\r\n");

        using var stream =
            new MemoryStream(data);

        var reader =
            new RespStreamReader(
                new RespParser());

        await Assert.ThrowsAsync<FormatException>(
            async () =>
            {
                await reader.ReadAsync(
                    stream,
                    CancellationToken.None);
            });
    }

    [Fact]
    public async Task ReadAsync_PreservesBufferedPipelinedCommands() {
        byte[] firstPart =
            Encoding.UTF8.GetBytes(
                "*1\r\n$4\r\nPING\r\n" +
                "*1\r\n$4\r\nPO");

        byte[] secondPart =
            Encoding.UTF8.GetBytes(
                "NG\r\n");

        using var stream =
            new ChunkedReadStream(
            [
                firstPart,
                secondPart
            ]);

        var reader =
            new RespStreamReader(
                new RespParser());

        RespValue? first =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        RespValue? second =
            await reader.ReadAsync(
                stream,
                CancellationToken.None);

        AssertPingCommand(first);

        AssertPongCommand(second);
    }

    private static void AssertPingCommand(
    RespValue? value) {
        var array =
            Assert.IsType<RespArray>(value);

        Assert.NotNull(array.Values);

        Assert.Single(array.Values);

        Assert.Equal(
            new RespBulkString("PING"),
            array.Values[0]);
    }

    private static void AssertPongCommand(
    RespValue? value) {
        var array =
            Assert.IsType<RespArray>(value);

        Assert.NotNull(array.Values);

        Assert.Single(array.Values);

        Assert.Equal(
            new RespBulkString("PONG"),
            array.Values[0]);
    }

    private sealed class ChunkedReadStream : Stream {
        private readonly IReadOnlyList<byte[]> _chunks;

        private int _currentChunk;

        public ChunkedReadStream(
            IReadOnlyList<byte[]> chunks) {
            _chunks = chunks;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            if (_currentChunk >= _chunks.Count) {
                return ValueTask.FromResult(0);
            }

            byte[] chunk =
                _chunks[_currentChunk++];

            if (chunk.Length > buffer.Length) {
                throw new InvalidOperationException(
                    "Test chunk is larger than the supplied buffer.");
            }

            chunk.CopyTo(buffer);

            return ValueTask.FromResult(
                chunk.Length);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            return ReadAsync(
                    buffer.AsMemory(
                        offset,
                        count),
                    cancellationToken)
                .AsTask();
        }

        public override void Flush() {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(
            long value) {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count) {
            throw new NotSupportedException();
        }
    }
}
