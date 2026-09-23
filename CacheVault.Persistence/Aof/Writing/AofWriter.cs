using CacheVault.Protocol.Resp.Abstractions;

namespace CacheVault.Persistence.Aof.Writing;

public sealed class AofWriter : IAsyncDisposable {
    private readonly AofCommandWriter _commandWriter;
    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private FileStream? _stream;
    private bool _disposed;

    public AofWriter(
        string filePath,
        IRespSerializer serializer) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        ArgumentNullException.ThrowIfNull(
            serializer);

        _filePath = filePath;

        _commandWriter =
            new AofCommandWriter(serializer);
    }

    public async ValueTask AppendCommandAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandName);

        ArgumentNullException.ThrowIfNull(
            arguments);

        ThrowIfDisposed();

        await _writeLock.WaitAsync(
            cancellationToken);

        try {
            ThrowIfDisposed();

            _stream ??= OpenStream();

            _commandWriter.WriteCommand(
                _stream,
                commandName,
                arguments);

            await _stream.FlushAsync(
                cancellationToken);
        }
        finally {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        await _writeLock.WaitAsync();

        try {
            if (_disposed) {
                return;
            }

            if (_stream is not null) {
                await _stream.FlushAsync();

                await _stream.DisposeAsync();

                _stream = null;
            }

            _disposed = true;
        }
        finally {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }

    private FileStream OpenStream() {
        string? directory =
            Path.GetDirectoryName(
                _filePath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(
                directory);
        }

        return new FileStream(
            _filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}