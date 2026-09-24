using CacheVault.Persistence.Aof.Writing;
using CacheVault.Persistence.Rdb.Writing;
using CacheVault.Server.Configuration;

namespace CacheVault.Server.Recovery;

public sealed class RdbPersistenceService
{
    private readonly ServerOptions _options;
    private readonly RdbSnapshotWriter _snapshotWriter;
    private readonly AofWriter? _aofWriter;

    public RdbPersistenceService(
        ServerOptions options,
        RdbSnapshotWriter snapshotWriter,
        AofWriter? aofWriter = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(snapshotWriter);

        _options = options;
        _snapshotWriter = snapshotWriter;
        _aofWriter = aofWriter;
    }

    public async Task SaveAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableRdb)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        long aofOffset =
            _aofWriter is null
                ? 0
                : await _aofWriter.GetLengthAsync(
                    cancellationToken);

        string directory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    _options.RdbFilePath)) ?? ".";

        Directory.CreateDirectory(directory);

        string temporaryPath =
            _options.RdbFilePath + ".tmp";

        await using (
            FileStream stream =
                new(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous))
        {
            _snapshotWriter.Write(
                stream,
                DateTimeOffset.UtcNow,
                aofOffset);

            await stream.FlushAsync(
                cancellationToken);
        }

        File.Move(
            temporaryPath,
            _options.RdbFilePath,
            overwrite: true);
    }
}
