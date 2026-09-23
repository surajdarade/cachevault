using CacheVault.Persistence.Aof.Reading;
using CacheVault.Persistence.Recovery;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Server.Commands.Replay;
using CacheVault.Server.Configuration;

namespace CacheVault.Server.Recovery;

public sealed class RecoveryCoordinator {
    private readonly RdbSnapshotLoader _rdbSnapshotLoader;
    private readonly AofCommandReplayer _aofCommandReplayer;

    public RecoveryCoordinator(
        RdbSnapshotLoader rdbSnapshotLoader,
        AofCommandReplayer aofCommandReplayer) {
        ArgumentNullException.ThrowIfNull(rdbSnapshotLoader);
        ArgumentNullException.ThrowIfNull(aofCommandReplayer);

        _rdbSnapshotLoader = rdbSnapshotLoader;
        _aofCommandReplayer = aofCommandReplayer;
    }

    public async ValueTask<RecoveryResult> RecoverAsync(
        ServerOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(options);

        int rdbRecordsLoaded = 0;
        int aofCommandsReplayed = 0;

        if (options.EnableRdb &&
            File.Exists(options.RdbFilePath)) {
            await using FileStream rdbStream =
                new(
                    options.RdbFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            rdbRecordsLoaded =
                _rdbSnapshotLoader.Load(
                    rdbStream,
                    now);
        }

        if (options.EnableAof &&
            File.Exists(options.AofFilePath)) {
            await using FileStream aofStream =
                new(
                    options.AofFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            aofCommandsReplayed =
                await _aofCommandReplayer.ReplayAsync(
                    aofStream,
                    cancellationToken);
        }

        return new RecoveryResult(
            rdbRecordsLoaded,
            aofCommandsReplayed);
    }
}