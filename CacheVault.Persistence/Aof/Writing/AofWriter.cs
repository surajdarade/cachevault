using CacheVault.Protocol.Resp.Abstractions;

namespace CacheVault.Persistence.Aof.Writing;

public sealed class AofWriter : IAsyncDisposable {
    private readonly AofCommandWriter _commandWriter;
    private readonly FileStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private bool _disposed;

    public AofWriter(
        string filePath,
        IRespSerializer serializer) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(serializer);

        string? directory =
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        _commandWriter =
            new AofCommandWriter(serializer);

        _stream =
            new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous);
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

            await _stream.FlushAsync();

            await _stream.DisposeAsync();

            _disposed = true;
        }
        finally {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}